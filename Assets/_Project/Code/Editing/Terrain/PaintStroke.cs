using System;
using System.Collections.Generic;
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
    /// only. Committed by <see cref="TerrainEdits.Commit(EditSession, IPaintEdit)"/> as one undo step.
    /// </summary>
    public sealed class PaintStroke : IPaintEdit
    {
        readonly PaintCanvas _canvas;

        /// <param name="store">World to paint; the terrain of <paramref name="space"/> is snapshotted now.</param>
        /// <param name="layer">The ground layer painted.</param>
        /// <param name="unpainted">The ground layer unpainted ground shows, which it becomes once painted beside another.</param>
        public PaintStroke(IRecordStore store, RecordId space, RecordId layer, BrushShape brush, RecordId unpainted)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (!(store.TryGet(layer, out var record) && record is GroundLayerRecord ground))
                throw new ArgumentException($"There is no ground layer '{layer}'.", nameof(layer));

            _canvas = new PaintCanvas(store, space, unpainted);
            Layer = layer;
            LayerName = ground.DisplayName;
            Brush = brush ?? throw new ArgumentNullException(nameof(brush));
        }

        public RecordId Layer { get; }

        public string LayerName { get; }

        public BrushShape Brush { get; }

        public string Description => $"Paint ({LayerName})";

        /// <summary>Chunks painted so far.</summary>
        public IReadOnlyCollection<ChunkCoord> TouchedChunks => _canvas.TouchedChunks;

        /// <summary>Applies one dab centered at world XZ. Returns the chunks it changed.</summary>
        public IReadOnlyCollection<ChunkCoord> ApplyDab(Vector2 center)
        {
            var radius = Brush.Radius;
            var minX = PaintCanvas.TexelOf(center.X - radius);
            var maxX = PaintCanvas.TexelOf(center.X + radius);
            var minZ = PaintCanvas.TexelOf(center.Y - radius);
            var maxZ = PaintCanvas.TexelOf(center.Y + radius);

            var changed = new HashSet<ChunkCoord>();
            for (var gz = minZ; gz <= maxZ; gz++)
            for (var gx = minX; gx <= maxX; gx++)
            {
                var texelCenter = new Vector2(PaintCanvas.CenterOf(gx), PaintCanvas.CenterOf(gz));
                var amount = Brush.Weight(Vector2.Distance(center, texelCenter)) * Brush.Strength;
                if (amount <= 0f)
                    continue;

                if (_canvas.Paint(gx, gz, Layer, amount) is ChunkCoord coord)
                    changed.Add(coord);
            }

            return changed;
        }

        /// <summary>The paint of a chunk as the stroke left it, or its original paint (null if unpainted) when untouched.</summary>
        public ChunkPaint PaintOf(ChunkCoord coord) => _canvas.PaintOf(coord);

        /// <summary>The touched chunks with their new paint, heights untouched.</summary>
        public IReadOnlyList<TerrainChunkRecord> ChangedRecords() => _canvas.ChangedRecords();
    }
}
