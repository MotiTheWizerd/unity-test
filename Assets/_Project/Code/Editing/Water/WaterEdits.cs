using System;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Terrain;

namespace Forge.Editing.Water
{
    /// <summary>
    /// Adds, raises or lowers, and removes lakes in the active plugin, each as one undo step. A lake must hold water
    /// at its point and must not spill, so every edit is checked against the terrain first; a refusal says why and
    /// what would work.
    /// </summary>
    public static class WaterEdits
    {
        /// <summary>Adds a lake at <paramref name="point"/> in <paramref name="space"/> with its surface at <paramref name="level"/>.</summary>
        public static LakeRecord AddLake(EditSession session, RecordId space, Vector2 point, float level)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var extent = Check(session, space, point, level);
            var lake = new LakeRecord(session.NewId(LakeRecord.IdPrefix), space, point.X, point.Y, level);
            session.Put(lake, Describe("Add lake", level, extent));
            return lake;
        }

        /// <summary>Moves lake <paramref name="lakeId"/>'s surface to <paramref name="level"/>. Returns false when it already is there.</summary>
        public static bool SetLakeLevel(EditSession session, RecordId lakeId, float level)
        {
            var lake = Find(session, lakeId);
            if (lake.Level == level)
                return false;

            var extent = Check(session, lake.Space, new Vector2(lake.X, lake.Z), level);
            session.Put(lake.WithLevel(level), Describe("Lake level", level, extent));
            return true;
        }

        /// <summary>Removes lake <paramref name="lakeId"/>.</summary>
        public static void RemoveLake(EditSession session, RecordId lakeId)
        {
            var lake = Find(session, lakeId);
            session.Delete(lake.Id, "Remove lake");
        }

        static LakeRecord Find(EditSession session, RecordId lakeId)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (session.World.Records.TryGet(lakeId, out var record) && record is LakeRecord lake)
                return lake;
            throw new ArgumentException($"There is no lake '{lakeId}'.", nameof(lakeId));
        }

        // The water a lake there would hold; refused with what would work when it would hold none or spill.
        static LakeExtent Check(EditSession session, RecordId space, Vector2 point, float level)
        {
            if (!(session.World.Records.TryGet(space, out var record) && record is SpaceRecord spaceRecord && spaceRecord.Kind == SpaceKind.Worldspace))
                throw new ArgumentException($"Lakes lie in worldspaces, and '{space}' isn't one.", nameof(space));

            var terrain = new TerrainGrid(session.World.Records, space);
            var at = FormattableString.Invariant($"({point.X:0.#}, {point.Y:0.#})");
            var (gx, gz) = LakeFill.NearestSample(terrain, point);
            if (!(terrain.SampleHeight(gx, gz) is float ground))
                throw new ArgumentException($"There is no terrain at {at}.", nameof(point));
            if (level <= ground)
                throw new ArgumentException(FormattableString.Invariant($"The ground at {at} is at {ground:0.##} m, so a lake there must stand higher than that."), nameof(level));

            var extent = LakeFill.Fill(terrain, point, level);
            // Only a refusal needs the rim's height, which costs a second, wider search.
            if (extent.Spills && LakeFill.Survey(terrain, point) is LakeBasin basin)
                throw new ArgumentException(basin.HoldsWater
                    ? FormattableString.Invariant($"Water at {level:0.##} m would run off the land: the hollow at {at} holds water only up to {basin.Brim:0.##} m, where its rim is lowest.")
                    : FormattableString.Invariant($"The ground at {at} isn't in a hollow, so water there would run off; dig or sculpt a hollow first."), nameof(level));
            return extent;
        }

        static string Describe(string action, float level, LakeExtent extent) =>
            FormattableString.Invariant($"{action}: {level:0.##} m, up to {extent.Deepest:0.#} m deep");
    }
}
