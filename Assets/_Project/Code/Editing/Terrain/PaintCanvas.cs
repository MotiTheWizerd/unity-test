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
    /// Working copies of the ground paint of the chunks a paint edit touches, addressed by world texel (one per meter,
    /// texel (gx, gz) covering gx..gx+1, gz..gz+1 m), each started from its chunk's paint on first touch, on land only.
    /// The brush (<see cref="PaintStroke"/>), shape painting (<see cref="GroundPainting"/>) and area auto paint
    /// (<see cref="AutoPainting"/>) all paint through one.
    /// </summary>
    sealed class PaintCanvas
    {
        const int Resolution = GroundPaint.Resolution;
        const int Texels = GroundPaint.TexelCount;

        /// <summary>Meters per texel.</summary>
        public const float TexelSize = WorldGrid.ChunkSize / Resolution;

        /// <summary>The most a painted shape may span, east-west or north-south, in meters (its soft edge may reach past it).</summary>
        public const float MaxSpan = 512f;

        readonly TerrainGrid _terrain;
        readonly RecordId _unpainted;
        readonly Dictionary<ChunkCoord, Working> _working = new Dictionary<ChunkCoord, Working>();

        /// <param name="store">World to paint; the terrain of <paramref name="space"/> is snapshotted now.</param>
        /// <param name="unpainted">The ground layer unpainted ground shows, which it becomes once painted beside another.</param>
        public PaintCanvas(IRecordStore store, RecordId space, RecordId unpainted)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (unpainted.IsDefault)
                throw new ArgumentException("The ground layer unpainted ground shows is required.", nameof(unpainted));

            _terrain = new TerrainGrid(store, space);
            _unpainted = unpainted;
        }

        /// <summary>Chunks painted so far.</summary>
        public IReadOnlyCollection<ChunkCoord> TouchedChunks => _working.Keys;

        /// <summary>The land painted, as it was when the canvas was made.</summary>
        public TerrainGrid Terrain => _terrain;

        /// <summary>The world texel, along one axis, that holds the position <paramref name="meters"/>.</summary>
        public static int TexelOf(float meters) => (int)Math.Floor(meters / TexelSize);

        /// <summary>The middle of world texel <paramref name="texel"/> along one axis, in meters.</summary>
        public static float CenterOf(int texel) => (texel + 0.5f) * TexelSize;

        /// <summary>
        /// Every world texel in the box around <paramref name="shape"/> grown by <paramref name="grow"/> meters, with its
        /// center's signed distance to the shape's outline (negative inside). Refuses a shape wider than <see cref="MaxSpan"/>
        /// (its outline; the soft edge may reach past it).
        /// </summary>
        public static IEnumerable<(int Gx, int Gz, float Outside)> TexelsOf(GroundShape shape, float grow)
        {
            if (shape.Span.X > MaxSpan || shape.Span.Y > MaxSpan)
                throw new ArgumentException(FormattableString.Invariant(
                    $"That shape spans {shape.Span.X:0} × {shape.Span.Y:0} m; paint at most {MaxSpan:0} m across at a time."));

            var min = shape.Min - new Vector2(grow);
            var max = shape.Max + new Vector2(grow);

            return Walk();

            IEnumerable<(int, int, float)> Walk()
            {
                for (var gz = TexelOf(min.Y); gz <= TexelOf(max.Y); gz++)
                for (var gx = TexelOf(min.X); gx <= TexelOf(max.X); gx++)
                    yield return (gx, gz, shape.SignedDistance(new Vector2(CenterOf(gx), CenterOf(gz))));
            }
        }

        /// <summary>
        /// Moves world texel (<paramref name="gx"/>, <paramref name="gz"/>) toward <paramref name="layer"/> by
        /// <paramref name="amount"/> (0..1): every layer keeps (1 − amount) of its share and the painted one gains the
        /// rest. Returns the texel's chunk, or null off the land, where nothing is painted.
        /// </summary>
        public ChunkCoord? Paint(int gx, int gz, RecordId layer, float amount)
        {
            var coord = new ChunkCoord(FloorDiv(gx, Resolution), FloorDiv(gz, Resolution));
            var working = WorkingFor(coord);
            if (working == null)
                return null;

            working.Paint(gx - coord.X * Resolution, gz - coord.Z * Resolution, working.IndexOf(layer), amount);
            return coord;
        }

        /// <summary>
        /// Moves world texel (<paramref name="gx"/>, <paramref name="gz"/>) toward a mix of <paramref name="layers"/>
        /// (their <paramref name="shares"/>, adding up to 1) by <paramref name="amount"/> (0..1): every layer keeps
        /// (1 − amount) of its share and the mix's layers gain the rest by their shares. Returns the texel's chunk, or
        /// null off the land, where nothing is painted.
        /// </summary>
        public ChunkCoord? Blend(int gx, int gz, IReadOnlyList<RecordId> layers, IReadOnlyList<float> shares, float amount)
        {
            var coord = new ChunkCoord(FloorDiv(gx, Resolution), FloorDiv(gz, Resolution));
            var working = WorkingFor(coord);
            if (working == null)
                return null;

            working.Blend(gx - coord.X * Resolution, gz - coord.Z * Resolution, layers, shares, amount);
            return coord;
        }

        /// <summary>The paint of a chunk as painted so far, or its original paint (null if unpainted) when untouched.</summary>
        public ChunkPaint PaintOf(ChunkCoord coord)
        {
            if (_working.TryGetValue(coord, out var working))
                return working.Result();
            return _terrain.TryGetChunk(coord, out var chunk) ? chunk.Paint : null;
        }

        /// <summary>The touched chunks with their new paint, heights untouched.</summary>
        public IReadOnlyList<TerrainChunkRecord> ChangedRecords() =>
            _working.Select(pair => pair.Value.Original.WithPaint(pair.Value.Result())).ToList();

        // The working copy of a chunk, started from its paint on first touch; null where there is no land.
        Working WorkingFor(ChunkCoord coord)
        {
            if (_working.TryGetValue(coord, out var working))
                return working;
            if (!_terrain.TryGetChunk(coord, out var chunk))
                return null;

            working = new Working(chunk, _unpainted);
            _working.Add(coord, working);
            return working;
        }

        static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);

        // A chunk's paint as fractions per layer (each texel's add up to 1), growing a plane for each new layer.
        sealed class Working
        {
            readonly List<RecordId> _layers = new List<RecordId>();
            readonly List<float[]> _planes = new List<float[]>();
            GroundPaint _result;

            public Working(TerrainChunkRecord original, RecordId unpainted)
            {
                Original = original;
                var paint = original.Paint?.Load();
                if (paint == null)
                {
                    _layers.Add(unpainted);
                    _planes.Add(Enumerable.Repeat(1f, Texels).ToArray());
                    return;
                }

                for (var layer = 0; layer < paint.Layers.Count; layer++)
                {
                    var plane = new float[Texels];
                    for (var z = 0; z < Resolution; z++)
                    for (var x = 0; x < Resolution; x++)
                        plane[z * Resolution + x] = paint[layer, x, z] / (float)GroundPaint.Full;
                    _layers.Add(paint.Layers[layer]);
                    _planes.Add(plane);
                }
            }

            public TerrainChunkRecord Original { get; }

            public int IndexOf(RecordId layer)
            {
                var index = _layers.IndexOf(layer);
                if (index >= 0)
                    return index;
                _layers.Add(layer);
                _planes.Add(new float[Texels]);
                return _layers.Count - 1;
            }

            // Moves texel (x, z) toward `layer` by `amount`: every layer keeps (1 − amount) of its share, the
            // painted one gains the rest, so the texel still adds up to 1.
            public void Paint(int x, int z, int layer, float amount)
            {
                var texel = z * Resolution + x;
                for (var i = 0; i < _planes.Count; i++)
                    _planes[i][texel] *= 1f - amount;
                _planes[layer][texel] += amount;
                _result = null;
            }

            // The same toward a mix of layers, each gaining its share of `amount`.
            public void Blend(int x, int z, IReadOnlyList<RecordId> layers, IReadOnlyList<float> shares, float amount)
            {
                var texel = z * Resolution + x;
                for (var i = 0; i < _planes.Count; i++)
                    _planes[i][texel] *= 1f - amount;
                for (var i = 0; i < layers.Count; i++)
                {
                    if (shares[i] > 0f)
                        _planes[IndexOf(layers[i])][texel] += amount * shares[i];
                }

                _result = null;
            }

            public GroundPaint Result()
            {
                if (_result != null)
                    return _result;

                var weights = new float[_planes.Count * Texels];
                for (var i = 0; i < _planes.Count; i++)
                    Array.Copy(_planes[i], 0, weights, i * Texels, Texels);
                return _result = GroundPaint.FromWeights(_layers, weights);
            }
        }
    }
}
