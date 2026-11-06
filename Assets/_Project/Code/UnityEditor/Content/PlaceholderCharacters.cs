using UnityEngine;
using static Forge.UnityEditor.Content.PlaceholderMaterials;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Stand-ins for characters until real ones come from Blender: built from primitives like the placeholder objects,
    /// but not placeable (no record), so they live apart from <see cref="PlaceholderContent"/>. The pivot is the feet,
    /// the front faces +Z, and a dark band across the face shows which way that is.
    /// </summary>
    static class PlaceholderCharacters
    {
        /// <summary>The player's mannequin (prefab name), 1.8 m tall, matching the player profile's body.</summary>
        public const string PlayerMannequin = "player_mannequin";

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // A capsule of a unit primitive is 2 m tall and 1 m across; scale y is half the height, x and z the diameter.
        public static void Mannequin(PrefabBuilder b) => b
            .Part(PrimitiveType.Capsule, Leather, V(-0.1f, 0.45f, 0f), V(0.17f, 0.45f, 0.17f))
            .Part(PrimitiveType.Capsule, Leather, V(0.1f, 0.45f, 0f), V(0.17f, 0.45f, 0.17f))
            .Part(PrimitiveType.Capsule, Tunic, V(0f, 1.18f, 0f), V(0.42f, 0.4f, 0.26f))
            .Part(PrimitiveType.Capsule, Tunic, V(-0.28f, 1.12f, 0f), V(0.12f, 0.33f, 0.12f), V(0f, 0f, -8f))
            .Part(PrimitiveType.Capsule, Tunic, V(0.28f, 1.12f, 0f), V(0.12f, 0.33f, 0.12f), V(0f, 0f, 8f))
            .Cube(Leather, V(0f, 0.95f, 0f), V(0.4f, 0.07f, 0.27f))
            .Sphere(Skin, V(0f, 1.67f, 0f), V(0.24f, 0.26f, 0.24f))
            .Cube(Window, V(0f, 1.69f, 0.11f), V(0.17f, 0.045f, 0.04f));
    }
}
