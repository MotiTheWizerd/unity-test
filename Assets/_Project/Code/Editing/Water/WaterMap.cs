using System;
using System.Numerics;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Editing.Terrain;

namespace Forge.Editing.Water
{
    /// <summary>
    /// The water over a space's land, as data: where it is, how high its surface stands and how deep it is. The one
    /// question the editor, the agent and later the player and NPCs (swimming) all ask. Engine-free, so it answers
    /// for any position, whether or not the scene has that chunk loaded. For now the only water is the sea (the
    /// space's <see cref="SpaceRecord.WaterLevel"/>); lakes join it here.
    /// </summary>
    public sealed class WaterMap
    {
        readonly TerrainGrid _terrain;

        public WaterMap(TerrainGrid terrain, float? seaLevel)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            SeaLevel = seaLevel;
        }

        /// <summary>The water of <paramref name="terrain"/>'s space as <paramref name="store"/> defines it; none if the space has no record.</summary>
        public static WaterMap Of(IRecordStore store, TerrainGrid terrain)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain));

            var space = store.TryGet(terrain.Space, out var record) ? record as SpaceRecord : null;
            return new WaterMap(terrain, space?.WaterLevel);
        }

        /// <summary>Height of the sea's surface in meters, or null for no sea.</summary>
        public float? SeaLevel { get; }

        /// <summary>
        /// Height of the water surface over <paramref name="ground"/>, or null where it is dry. Beyond the terrain the
        /// sea goes on (open sea), so there it is the sea level.
        /// </summary>
        public float? SurfaceAt(Vector2 ground)
        {
            if (!(SeaLevel is float sea))
                return null;
            return _terrain.HeightAt(ground) is float height && height >= sea ? (float?)null : sea;
        }

        /// <summary>
        /// Meters of water over the ground at <paramref name="ground"/>: 0 where it is dry, null where there is no
        /// terrain to measure from (beyond the land's edge).
        /// </summary>
        public float? DepthAt(Vector2 ground)
        {
            if (!(_terrain.HeightAt(ground) is float height))
                return null;
            return SeaLevel is float sea && sea > height ? sea - height : 0f;
        }
    }
}
