using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// One ground paint stroke: dabs of one ground layer into a working copy of the paint of the chunks it touches,
    /// as <see cref="SculptStroke"/> does for heights. Each dab moves every texel under the brush toward the layer by
    /// the brush's weight there (the other layers give up their share in proportion), across chunk edges, on land
    /// only. Committed by <see cref="TerrainEdits.Commit(EditSession, PaintStroke)"/> as one undo step.
    /// </summary>
    public sealed class PaintStroke
    {
        const int Resolution = GroundPaint.Resolution;
        const int Texels = GroundPaint.TexelCount;
        const float TexelSize = WorldGrid.ChunkSize / Resolution;

        readonly TerrainGrid _terrain;
        readonly RecordId _unpainted;
        readonly Dictionary<ChunkCoord, Working> _working = new Dictionary<ChunkCoord, Working>();

        /// <param name="store">World to paint; the terrain of <paramref name="space"/> is snapshotted now.</param>
        /// <param name="layer">The ground layer painted.</param>
        /// <param name="unpainted">The ground layer unpainted ground shows, which it becomes once painted beside another.</param>
        public PaintStroke(IRecordStore store, RecordId space, RecordId layer, BrushShape brush, RecordId unpainted)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (!(store.TryGet(layer, out var record) && record is GroundLayerRecord ground))
                throw new ArgumentException($"There is no ground layer '{layer}'.", nameof(layer));
            if (unpainted.IsDefault)
                throw new ArgumentException("The ground layer unpainted ground shows is required.", nameof(unpainted));

            _terrain = new TerrainGrid(store, space);
            Layer = layer;
            LayerName = ground.DisplayName;
            Brush = brush ?? throw new ArgumentNullException(nameof(brush));
            _unpainted = unpainted;
        }

        public RecordId Layer { get; }

        public string LayerName { get; }

        public BrushShape Brush { get; }

        public string Description => $"Paint ({LayerName})";

        /// <summary>Chunks painted so far.</summary>
        public IReadOnlyCollection<ChunkCoord> TouchedChunks => _working.Keys;

        /// <summary>Applies one dab centered at world XZ. Returns the chunks it changed.</summary>
        public IReadOnlyCollection<ChunkCoord> ApplyDab(Vector2 center)
        {
            var radius = Brush.Radius;
            var minX = (int)Math.Floor((center.X - radius) / TexelSize);
            var maxX = (int)Math.Floor((center.X + radius) / TexelSize);
            var minZ = (int)Math.Floor((center.Y - radius) / TexelSize);
            var maxZ = (int)Math.Floor((center.Y + radius) / TexelSize);

            var changed = new HashSet<ChunkCoord>();
            for (var gz = minZ; gz <= maxZ; gz++)
            for (var gx = minX; gx <= maxX; gx++)
            {
                var texelCenter = new Vector2((gx + 0.5f) * TexelSize, (gz + 0.5f) * TexelSize);
                var amount = Brush.Weight(Vector2.Distance(center, texelCenter)) * Brush.Strength;
                if (amount <= 0f)
                    continue;

                var coord = new ChunkCoord(FloorDiv(gx, Resolution), FloorDiv(gz, Resolution));
                var working = WorkingFor(coord);
                if (working == null)
                    continue;

                working.Paint(gx - coord.X * Resolution, gz - coord.Z * Resolution, working.IndexOf(Layer), amount);
                changed.Add(coord);
            }

            return changed;
        }

        /// <summary>The paint of a chunk as the stroke left it, or its original paint (null if unpainted) when untouched.</summary>
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
