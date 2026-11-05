using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>What a pick found, in world coordinates (see <see cref="ScenePicker"/>).</summary>
    public readonly struct WorldHit
    {
        public WorldHit(Vector3 point, float distance, Collider collider)
        {
            Point = point;
            Distance = distance;
            Collider = collider;
        }

        /// <summary>Where the ray struck, in world coordinates.</summary>
        public Vector3 Point { get; }

        /// <summary>How far along the ray, in meters.</summary>
        public float Distance { get; }

        public Collider Collider { get; }

        /// <summary>The hit is on the land (not an object, nor an interior's floor grid).</summary>
        public bool OnTerrain => Collider is TerrainCollider;
    }
}
