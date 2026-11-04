using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Editing.Terrain;

namespace Forge.Editing.Water
{
    /// <summary>
    /// The water over a space's land, as data: where it is, how high its surface stands and how deep it is. The one
    /// question the editor, the agent and later the player and NPCs (swimming) all ask. Engine-free, so it answers
    /// for any position, whether or not the scene has that chunk loaded. The water is the sea (the space's
    /// <see cref="SpaceRecord.WaterLevel"/>) and the lakes, each filled over the terrain from its point.
    /// </summary>
    public sealed class WaterMap
    {
        readonly TerrainGrid _terrain;

        public WaterMap(TerrainGrid terrain, float? seaLevel, IEnumerable<LakeWater> lakes = null)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            SeaLevel = seaLevel;
            Lakes = (lakes ?? Enumerable.Empty<LakeWater>()).ToList();
        }

        /// <summary>
        /// The water of <paramref name="terrain"/>'s space as <paramref name="store"/> defines it: its sea (none if the
        /// space has no record) and its lakes, each filled over the terrain.
        /// </summary>
        public static WaterMap Of(IRecordStore store, TerrainGrid terrain)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain));

            var space = store.TryGet(terrain.Space, out var record) ? record as SpaceRecord : null;
            var lakes = store.OfType<LakeRecord>()
                .Where(lake => lake.Space == terrain.Space)
                .OrderBy(lake => lake.Id)
                .Select(lake => new LakeWater(lake, LakeFill.Fill(terrain, new Vector2(lake.X, lake.Z), lake.Level)));
            return new WaterMap(terrain, space?.WaterLevel, lakes);
        }

        public TerrainGrid Terrain => _terrain;

        /// <summary>Height of the sea's surface in meters, or null for no sea.</summary>
        public float? SeaLevel { get; }

        /// <summary>The space's lakes and where their water lies (none for a lake that spills).</summary>
        public IReadOnlyList<LakeWater> Lakes { get; }

        /// <summary>
        /// Height of the water surface over <paramref name="ground"/>, or null where it is dry. Beyond the terrain the
        /// sea goes on (open sea), so there it is the sea level.
        /// </summary>
        public float? SurfaceAt(Vector2 ground) => SurfaceOver(ground, _terrain.HeightAt(ground));

        /// <summary>
        /// Meters of water over the ground at <paramref name="ground"/>: 0 where it is dry, null where there is no
        /// terrain to measure from (beyond the land's edge).
        /// </summary>
        public float? DepthAt(Vector2 ground)
        {
            if (!(_terrain.HeightAt(ground) is float height))
                return null;
            return SurfaceOver(ground, height) is float surface ? surface - height : 0f;
        }

        /// <summary>The lake whose water covers <paramref name="ground"/>, or null.</summary>
        public LakeWater LakeAt(Vector2 ground)
        {
            var height = _terrain.HeightAt(ground);
            return height.HasValue ? Lakes.Where(lake => Covers(lake, ground, height.Value)).OrderByDescending(lake => lake.Record.Level).FirstOrDefault() : null;
        }

        // The highest water standing over the ground here: the sea, and any lake whose water covers this spot.
        float? SurfaceOver(Vector2 ground, float? height)
        {
            if (!height.HasValue)
                return SeaLevel;

            var surface = SeaLevel is float sea && sea > height.Value ? sea : (float?)null;
            foreach (var lake in Lakes)
            {
                if (Covers(lake, ground, height.Value) && !(surface >= lake.Record.Level))
                    surface = lake.Record.Level;
            }

            return surface;
        }

        bool Covers(LakeWater lake, Vector2 ground, float height)
        {
            if (lake.Record.Level <= height || lake.Extent.IsEmpty)
                return false;
            var gx = (int)Math.Floor(ground.X / _terrain.Spacing);
            var gz = (int)Math.Floor(ground.Y / _terrain.Spacing);
            return lake.Extent.CoversCell(gx, gz);
        }
    }
}
