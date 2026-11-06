using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Geometry;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// Paints one ground layer over shapes on the ground: a band along a path, a rectangle or a polygon. Each shape
    /// paints every texel once (where a brush stroke's dabs build up), fully inside it and fading over a soft edge
    /// centered on its outline, so the painted shape keeps the size asked for. On land only, across chunk edges.
    /// Committed by <see cref="TerrainEdits.Commit(EditSession, IPaintEdit)"/> as one undo step.
    /// </summary>
    public sealed class GroundPainting : IPaintEdit
    {
        /// <summary>The most a shape may span, with its edge, east-west or north-south, in meters.</summary>
        public const float MaxSpan = 256f;

        /// <summary>The widest soft edge, in meters.</summary>
        public const float MaxEdge = 16f;

        readonly PaintCanvas _canvas;

        /// <param name="store">World to paint; the terrain of <paramref name="space"/> is snapshotted now.</param>
        /// <param name="layer">The ground layer painted.</param>
        /// <param name="unpainted">The ground layer unpainted ground shows, which it becomes once painted beside another.</param>
        public GroundPainting(IRecordStore store, RecordId space, RecordId layer, RecordId unpainted)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (!(store.TryGet(layer, out var record) && record is GroundLayerRecord ground))
                throw new ArgumentException($"There is no ground layer '{layer}'.", nameof(layer));

            _canvas = new PaintCanvas(store, space, unpainted);
            Layer = layer;
            LayerName = ground.DisplayName;
        }

        public RecordId Layer { get; }

        public string LayerName { get; }

        public string Description => $"Paint ({LayerName})";

        /// <summary>Chunks painted so far.</summary>
        public IReadOnlyCollection<ChunkCoord> TouchedChunks => _canvas.TouchedChunks;

        /// <summary>Texels (square meters) painted so far, each counted once per shape.</summary>
        public int PaintedTexels { get; private set; }

        /// <summary>
        /// A band <paramref name="width"/> meters wide along the path through <paramref name="points"/> (two or more),
        /// rounded at its ends and corners. Returns the texels painted.
        /// </summary>
        /// <param name="edge">Width of the soft edge, centered on the band's sides; 0 for a hard edge.</param>
        /// <param name="strength">How fully the ground takes the layer: 1 covers it, less lets the old ground show.</param>
        public int PaintPath(IReadOnlyList<Vector2> points, float width, float edge, float strength)
        {
            if (points == null || points.Count < 2)
                throw new ArgumentException("A path needs at least two points.", nameof(points));
            if (!(width > 0f))
                throw new ArgumentOutOfRangeException(nameof(width), width, "The path's width must be positive.");

            var half = width / 2f;
            return PaintShape(points, half, point => DistanceToPath(point, points) - half, edge, strength);
        }

        /// <summary>The rectangle <paramref name="area"/>. Returns the texels painted.</summary>
        /// <inheritdoc cref="PaintPath" select="param[@name='edge']|param[@name='strength']"/>
        public int PaintRect(GroundRect area, float edge, float strength) =>
            PaintShape(area.Corners(), 0f, point => SignedDistance(area, point), edge, strength);

        /// <summary>
        /// The polygon with <paramref name="corners"/> (three or more, in order around it; where its sides cross, the
        /// even-odd rule decides what is inside). Returns the texels painted.
        /// </summary>
        /// <inheritdoc cref="PaintPath" select="param[@name='edge']|param[@name='strength']"/>
        public int PaintPolygon(IReadOnlyList<Vector2> corners, float edge, float strength)
        {
            if (corners == null || corners.Count < 3)
                throw new ArgumentException("An area needs at least three corners.", nameof(corners));

            return PaintShape(corners, 0f, point => Inside(point, corners) ? -DistanceToRing(point, corners) : DistanceToRing(point, corners), edge, strength);
        }

        /// <summary>The paint of a chunk as painted so far, or its original paint (null if unpainted) when untouched.</summary>
        public ChunkPaint PaintOf(ChunkCoord coord) => _canvas.PaintOf(coord);

        /// <summary>The touched chunks with their new paint, heights untouched.</summary>
        public IReadOnlyList<TerrainChunkRecord> ChangedRecords() => _canvas.ChangedRecords();

        // Paints every texel within reach of `outline` (its points, grown by `reach` and half the edge) by the signed
        // distance from the shape's outline to its center (negative inside): full from half an edge inside, half on
        // the outline, nothing from half an edge beyond it.
        int PaintShape(IReadOnlyList<Vector2> outline, float reach, Func<Vector2, float> signedDistance, float edge, float strength)
        {
            if (!(edge >= 0f && edge <= MaxEdge))
                throw new ArgumentOutOfRangeException(nameof(edge), edge, $"The soft edge must be within [0, {MaxEdge}] m.");
            if (!(strength > 0f && strength <= 1f))
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "Strength must be within (0, 1].");

            var grow = reach + edge / 2f;
            var min = new Vector2(outline.Min(p => p.X) - grow, outline.Min(p => p.Y) - grow);
            var max = new Vector2(outline.Max(p => p.X) + grow, outline.Max(p => p.Y) + grow);
            if (max.X - min.X > MaxSpan || max.Y - min.Y > MaxSpan)
                throw new ArgumentException(FormattableString.Invariant(
                    $"That shape spans {max.X - min.X:0} × {max.Y - min.Y:0} m; paint at most {MaxSpan:0} m across at a time."));

            var painted = 0;
            for (var gz = PaintCanvas.TexelOf(min.Y); gz <= PaintCanvas.TexelOf(max.Y); gz++)
            for (var gx = PaintCanvas.TexelOf(min.X); gx <= PaintCanvas.TexelOf(max.X); gx++)
            {
                var outside = signedDistance(new Vector2(PaintCanvas.CenterOf(gx), PaintCanvas.CenterOf(gz)));
                var coverage = edge > 0f ? Falloff.Smooth((outside + edge / 2f) / edge) : outside <= 0f ? 1f : 0f;
                if (coverage > 0f && _canvas.Paint(gx, gz, Layer, coverage * strength) != null)
                    painted++;
            }

            PaintedTexels += painted;
            return painted;
        }

        // Outside: how far from the rectangle; inside, negative: how far in from its nearest side.
        static float SignedDistance(GroundRect area, Vector2 point)
        {
            var outside = area.DistanceOutside(point);
            if (outside > 0f)
                return outside;

            var offset = point - area.Center;
            var fromSides = area.Width / 2f - Math.Abs(Vector2.Dot(offset, area.Across));
            var fromEnds = area.Depth / 2f - Math.Abs(Vector2.Dot(offset, area.Front));
            return -Math.Min(fromSides, fromEnds);
        }

        static float DistanceToPath(Vector2 point, IReadOnlyList<Vector2> points)
        {
            var nearest = float.MaxValue;
            for (var i = 1; i < points.Count; i++)
                nearest = Math.Min(nearest, DistanceToSegment(point, points[i - 1], points[i]));
            return nearest;
        }

        // Distance to the polygon's sides, the last corner joined back to the first.
        static float DistanceToRing(Vector2 point, IReadOnlyList<Vector2> corners)
        {
            var nearest = DistanceToSegment(point, corners[corners.Count - 1], corners[0]);
            for (var i = 1; i < corners.Count; i++)
                nearest = Math.Min(nearest, DistanceToSegment(point, corners[i - 1], corners[i]));
            return nearest;
        }

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var along = b - a;
            var lengthSquared = along.LengthSquared();
            var t = lengthSquared > 0f ? Math.Max(0f, Math.Min(1f, Vector2.Dot(point - a, along) / lengthSquared)) : 0f;
            return Vector2.Distance(point, a + along * t);
        }

        // Even-odd: a ray from the point eastwards crosses the sides an odd number of times when it is inside.
        static bool Inside(Vector2 point, IReadOnlyList<Vector2> corners)
        {
            var inside = false;
            for (int i = 0, j = corners.Count - 1; i < corners.Count; j = i++)
            {
                var a = corners[i];
                var b = corners[j];
                if ((a.Y > point.Y) != (b.Y > point.Y)
                    && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }

            return inside;
        }
    }
}
