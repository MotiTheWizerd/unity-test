using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Terrain;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    /// <summary>
    /// Land for water tests: 2 × 2 chunks of <see cref="Space"/> (global samples 0–128, 1 m apart), flat at 50 m, with a
    /// square pit at 40 m (samples 54–74 on both axes, 21 × 21) and a gutter at 45 m running from the pit east to
    /// the land's edge along z = 64. Water in the pit stays in up to 45 m; above that it runs out down the gutter.
    /// </summary>
    static class PitTerrain
    {
        public static readonly RecordId Space = Forge.Tests.Data.TestRecords.Space;

        public static readonly System.Numerics.Vector2 PitCenter = new System.Numerics.Vector2(64f, 64f);

        public const float Plain = 50f;
        public const float PitFloor = 40f;
        public const float Gutter = 45f;
        public const int PitSamples = 21 * 21;

        public static float HeightAt(int gx, int gz)
        {
            if (System.Math.Abs(gx - 64) <= 10 && System.Math.Abs(gz - 64) <= 10)
                return PitFloor;
            return gz == 64 && gx > 74 ? Gutter : Plain;
        }

        /// <summary>The space record (a worldspace, or <paramref name="kind"/>) and its four terrain chunks.</summary>
        public static Record[] Records(SpaceKind kind = SpaceKind.Worldspace, float? seaLevel = null)
        {
            var records = new List<Record> { new SpaceRecord(Space, "Pit Land", kind, Plain, seaLevel) };
            var samples = WorldGrid.DefaultHeightmapResolution;
            var cells = samples - 1;
            for (var cx = 0; cx <= 1; cx++)
            for (var cz = 0; cz <= 1; cz++)
            {
                var coord = new ChunkCoord(cx, cz);
                var (x0, z0) = (cx * cells, cz * cells);
                records.Add(new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, coord), Space, coord,
                    HeightField.FromFunction((sx, sz) => HeightAt(x0 + sx, z0 + sz), samples)));
            }

            return records.ToArray();
        }

        public static EditSession Session(params Record[] extra)
        {
            var records = new List<Record>(Records());
            records.AddRange(extra);
            return new EditSession(new[] { Plugin("core", records.ToArray()) }, Plugin("user", new[] { "core" }));
        }

        public static TerrainGrid Grid(EditSession session) => new TerrainGrid(session.World.Records, Space);
    }
}
