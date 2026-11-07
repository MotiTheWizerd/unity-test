using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Geometry;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// Paints an area by the land's shape, as <see cref="AutoPaint"/> paints a whole space (the same rules, the same
    /// result inside), and fades into the paint around it over a soft edge centered on the area's outline, so a
    /// mountain gets its rock and snow without the rest of the world being repainted. On land only, across chunk
    /// edges; committed by <see cref="TerrainEdits.Commit(EditSession, IPaintEdit)"/> as one undo step.
    /// </summary>
    public sealed class AutoPainting : IPaintEdit
    {
        /// <summary>The widest soft edge, in meters.</summary>
        public const float MaxEdge = GroundPainting.MaxEdge;

        readonly PaintCanvas _canvas;
        readonly AutoPaintRules _rules;
        readonly float? _seaLevel;
        readonly RecordId[] _roles;
        readonly float[] _painted = new float[AutoPaint.RoleCount];

        /// <param name="store">World to paint; the terrain of <paramref name="space"/> is snapshotted now.</param>
        /// <param name="seaLevel">The space's sea, for the shore; null for none.</param>
        /// <param name="unpainted">The ground layer unpainted ground shows, which it becomes once painted beside another.</param>
        public AutoPainting(IRecordStore store, RecordId space, AutoPaintRules rules, float? seaLevel, RecordId unpainted)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _canvas = new PaintCanvas(store, space, unpainted);
            _seaLevel = seaLevel;
            _roles = AutoPaint.Roles(rules);
        }

        public string Description => "Auto paint";

        /// <summary>Texels (square meters) painted so far, soft edges included.</summary>
        public int PaintedTexels { get; private set; }

        /// <summary>
        /// How much of what was painted each ground took, as shares adding up to 1, largest first (a ground serving two
        /// roles counted once).
        /// </summary>
        public IReadOnlyList<(RecordId Ground, float Share)> Mix()
        {
            var total = _painted.Sum();
            return _roles
                .Select((ground, role) => (ground, role))
                .GroupBy(pair => pair.ground, pair => _painted[pair.role])
                .Select(group => (group.Key, total > 0f ? group.Sum() / total : 0f))
                .Where(pair => pair.Item2 > 0f)
                .OrderByDescending(pair => pair.Item2)
                .ToList();
        }

        /// <summary>
        /// Paints <paramref name="area"/> by the land's shape: fully from half an edge inside its outline, fading to the
        /// old paint half an edge beyond it. Returns the texels painted.
        /// </summary>
        /// <param name="edge">Width of the soft edge, centered on the outline; 0 for a hard edge.</param>
        public int Paint(GroundShape area, float edge)
        {
            if (area == null)
                throw new ArgumentNullException(nameof(area));
            if (!(edge >= 0f && edge <= MaxEdge))
                throw new ArgumentOutOfRangeException(nameof(edge), edge, $"The soft edge must be within [0, {MaxEdge}] m.");

            var shares = new float[AutoPaint.RoleCount];
            var painted = 0;
            foreach (var (gx, gz, outside) in PaintCanvas.TexelsOf(area, edge / 2f))
            {
                var coverage = edge > 0f ? Falloff.Smooth((outside + edge / 2f) / edge) : outside <= 0f ? 1f : 0f;
                if (coverage <= 0f)
                    continue;

                var coord = new ChunkCoord(FloorDiv(gx, GroundPaint.Resolution), FloorDiv(gz, GroundPaint.Resolution));
                if (!(_canvas.Terrain.HeightsOf(coord) is HeightField heights))
                    continue;

                AutoPaint.Shares(heights, coord, gx - coord.X * GroundPaint.Resolution, gz - coord.Z * GroundPaint.Resolution, _rules, _seaLevel, shares);
                if (_canvas.Blend(gx, gz, _roles, shares, coverage) == null)
                    continue;

                for (var role = 0; role < shares.Length; role++)
                    _painted[role] += shares[role] * coverage;
                painted++;
            }

            PaintedTexels += painted;
            return painted;
        }

        /// <summary>The paint of a chunk as painted so far, or its original paint (null if unpainted) when untouched.</summary>
        public ChunkPaint PaintOf(ChunkCoord coord) => _canvas.PaintOf(coord);

        /// <summary>The touched chunks with their new paint, heights untouched.</summary>
        public IReadOnlyList<TerrainChunkRecord> ChangedRecords() => _canvas.ChangedRecords();

        static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
    }
}
