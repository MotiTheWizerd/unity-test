using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Geometry;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// Paints one ground layer over shapes on the ground (<see cref="GroundShape"/>: a band along a path, a rectangle
    /// or a polygon). Each shape paints every texel once (where a brush stroke's dabs build up), fully inside it and
    /// fading over a soft edge centered on its outline, so the painted shape keeps the size asked for. On land only,
    /// across chunk edges. Committed by <see cref="TerrainEdits.Commit(EditSession, IPaintEdit)"/> as one undo step.
    /// </summary>
    public sealed class GroundPainting : IPaintEdit
    {
        /// <summary>The most a shape may span, east-west or north-south, in meters (its soft edge may reach past it).</summary>
        public const float MaxSpan = PaintCanvas.MaxSpan;

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
        /// Paints <paramref name="shape"/> by each texel's signed distance to its outline: full from half an edge inside,
        /// half on the outline, nothing from half an edge beyond it. Returns the texels painted.
        /// </summary>
        /// <param name="edge">Width of the soft edge, centered on the outline; 0 for a hard edge.</param>
        /// <param name="strength">How fully the ground takes the layer: 1 covers it, less lets the old ground show.</param>
        public int Paint(GroundShape shape, float edge, float strength)
        {
            if (shape == null)
                throw new ArgumentNullException(nameof(shape));
            if (!(edge >= 0f && edge <= MaxEdge))
                throw new ArgumentOutOfRangeException(nameof(edge), edge, $"The soft edge must be within [0, {MaxEdge}] m.");
            if (!(strength > 0f && strength <= 1f))
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "Strength must be within (0, 1].");

            var painted = 0;
            foreach (var (gx, gz, outside) in PaintCanvas.TexelsOf(shape, edge / 2f))
            {
                var coverage = edge > 0f ? Falloff.Smooth((outside + edge / 2f) / edge) : outside <= 0f ? 1f : 0f;
                if (coverage > 0f && _canvas.Paint(gx, gz, Layer, coverage * strength) != null)
                    painted++;
            }

            PaintedTexels += painted;
            return painted;
        }

        /// <summary>The paint of a chunk as painted so far, or its original paint (null if unpainted) when untouched.</summary>
        public ChunkPaint PaintOf(ChunkCoord coord) => _canvas.PaintOf(coord);

        /// <summary>The touched chunks with their new paint, heights untouched.</summary>
        public IReadOnlyList<TerrainChunkRecord> ChangedRecords() => _canvas.ChangedRecords();
    }
}
