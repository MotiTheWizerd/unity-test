using System.Collections.Generic;
using Forge.Data.Records;
using UnityEngine;
using static Forge.UnityEditor.Content.PlaceholderMaterials;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The base game's placeable objects with placeholder art built from primitives. Real art
    /// replaces a placeholder by pointing its asset key at another prefab in the catalog;
    /// the records, and every object placed from them, stay as they are.
    /// </summary>
    static class PlaceholderContent
    {
        public static IReadOnlyList<ContentDefinition> All { get; } = new[]
        {
            new ContentDefinition("tree_pine", "Pine Tree", "Nature/Trees", "nature/tree_pine", PineTree, Varied(0.75f, 1.35f)),
            new ContentDefinition("tree_oak", "Oak Tree", "Nature/Trees", "nature/tree_oak", OakTree, Varied(0.8f, 1.3f)),
            new ContentDefinition("bush", "Bush", "Nature/Plants", "nature/bush", Bush, Varied(0.7f, 1.3f)),
            new ContentDefinition("rock_cluster", "Rock Cluster", "Nature/Rocks", "nature/rock_cluster", RockCluster, Varied(0.6f, 1.5f)),
            new ContentDefinition("house_small", "Small House", "Architecture/Buildings", "architecture/house_small", SmallHouse),
            new ContentDefinition("wall_stone", "Stone Wall", "Architecture/Walls", "architecture/wall_stone", StoneWall),
            new ContentDefinition("fence_wood", "Wooden Fence", "Architecture/Walls", "architecture/fence_wood", WoodenFence),
            new ContentDefinition("lamp_post", "Lamp Post", "Props/Lighting", "props/lamp_post", LampPost),
            new ContentDefinition("campfire", "Campfire", "Props/Lighting", "props/campfire", Campfire),
            new ContentDefinition("barrel", "Barrel", "Props/Containers", "props/barrel", Barrel),
            new ContentDefinition("bench", "Bench", "Props/Furniture", "props/bench", Bench),
            new ContentDefinition("signpost", "Signpost", "Props/Decor", "props/signpost", Signpost),
        };

        /// <summary>Natural things: any heading, a size within [min, max].</summary>
        public static PlacementVariation Varied(float minScale, float maxScale) => new PlacementVariation(true, minScale, maxScale);

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static void PineTree(PrefabBuilder b) => b
            .Post(Bark, Vector3.zero, 0.3f, 2.2f)
            .Sphere(LeavesDark, V(0, 2.4f, 0), V(2.4f, 1.4f, 2.4f))
            .Sphere(LeavesDark, V(0, 3.5f, 0), V(1.8f, 1.2f, 1.8f))
            .Sphere(LeavesDark, V(0, 4.5f, 0), V(1.1f, 1f, 1.1f))
            .Sphere(LeavesDark, V(0, 5.2f, 0), V(0.5f, 0.8f, 0.5f));

        static void OakTree(PrefabBuilder b) => b
            .Post(Bark, Vector3.zero, 0.5f, 2.6f)
            .Sphere(Leaves, V(0, 3.6f, 0), V(3.6f, 3f, 3.6f))
            .Sphere(Leaves, V(1.1f, 3.2f, 0.4f), V(2.2f, 2f, 2.2f))
            .Sphere(Leaves, V(-0.9f, 3.4f, -0.6f), V(2.4f, 2.1f, 2.4f));

        static void Bush(PrefabBuilder b) => b
            .Sphere(Leaves, V(0, 0.45f, 0), V(1.4f, 0.9f, 1.4f))
            .Sphere(Leaves, V(0.5f, 0.35f, 0.3f), V(1f, 0.7f, 1f))
            .Sphere(LeavesDark, V(-0.45f, 0.3f, -0.2f), V(0.9f, 0.6f, 0.9f));

        static void RockCluster(PrefabBuilder b) => b
            .Sphere(Stone, V(0, 0.45f, 0), V(1.6f, 1.1f, 1.3f), V(8, 20, 0))
            .Sphere(Stone, V(1.1f, 0.3f, 0.4f), V(0.9f, 0.7f, 0.8f), V(0, 45, 10))
            .Sphere(Stone, V(-0.7f, 0.22f, 0.7f), V(0.7f, 0.5f, 0.6f));

        // Walls 4 × 3 × 5 m; the roof is a cube turned 45° so its upper half forms a gable.
        static void SmallHouse(PrefabBuilder b) => b
            .Cube(Plaster, V(0, 1.5f, 0), V(4f, 3f, 5f))
            .Cube(Roof, V(0, 3f, 0), V(3.11f, 3.11f, 5.4f), V(0, 0, 45))
            .Cube(WoodDark, V(0, 0.9f, -2.51f), V(0.9f, 1.8f, 0.05f))
            .Cube(Window, V(1.25f, 1.8f, -2.51f), V(0.7f, 0.6f, 0.05f))
            .Cube(Window, V(-1.25f, 1.8f, -2.51f), V(0.7f, 0.6f, 0.05f))
            .Cube(Stone, V(1.2f, 4.4f, 1.3f), V(0.5f, 1.6f, 0.5f));

        static void StoneWall(PrefabBuilder b) => b
            .Cube(Stone, V(0, 1f, 0), V(4f, 2f, 0.6f))
            .Cube(Stone, V(0, 2.05f, 0), V(4.2f, 0.15f, 0.75f));

        static void WoodenFence(PrefabBuilder b) => b
            .Cube(Wood, V(-1.5f, 0.6f, 0), V(0.15f, 1.2f, 0.15f))
            .Cube(Wood, V(1.5f, 0.6f, 0), V(0.15f, 1.2f, 0.15f))
            .Cube(Wood, V(0, 0.9f, 0), V(3.1f, 0.1f, 0.06f))
            .Cube(Wood, V(0, 0.45f, 0), V(3.1f, 0.1f, 0.06f));

        static void LampPost(PrefabBuilder b) => b
            .Post(Metal, Vector3.zero, 0.35f, 0.2f)
            .Post(Metal, Vector3.zero, 0.12f, 3.5f)
            .Sphere(LampGlow, V(0, 3.7f, 0), V(0.45f, 0.45f, 0.45f))
            .Post(Metal, V(0, 3.92f, 0), 0.5f, 0.08f);

        static void Campfire(PrefabBuilder b)
        {
            for (var i = 0; i < 3; i++)
                b.Part(PrimitiveType.Cylinder, WoodDark, V(0, 0.12f, 0), V(0.18f, 0.55f, 0.18f), V(90, i * 60, 0));
            for (var i = 0; i < 7; i++)
            {
                var angle = i * Mathf.PI * 2f / 7f;
                b.Sphere(Stone, V(Mathf.Cos(angle) * 0.75f, 0.1f, Mathf.Sin(angle) * 0.75f), V(0.32f, 0.24f, 0.3f));
            }

            b.Sphere(Fire, V(0, 0.35f, 0), V(0.45f, 0.6f, 0.45f));
        }

        static void Barrel(PrefabBuilder b) => b
            .Post(Wood, Vector3.zero, 0.8f, 1.2f)
            .Post(Metal, V(0, 0.22f, 0), 0.82f, 0.06f)
            .Post(Metal, V(0, 0.92f, 0), 0.82f, 0.06f);

        static void Bench(PrefabBuilder b) => b
            .Cube(Wood, V(0, 0.45f, 0), V(1.6f, 0.08f, 0.45f))
            .Cube(Wood, V(0, 0.8f, 0.2f), V(1.6f, 0.4f, 0.06f))
            .Cube(WoodDark, V(0.7f, 0.21f, 0), V(0.08f, 0.42f, 0.4f))
            .Cube(WoodDark, V(-0.7f, 0.21f, 0), V(0.08f, 0.42f, 0.4f));

        static void Signpost(PrefabBuilder b) => b
            .Post(Wood, Vector3.zero, 0.1f, 2.2f)
            .Cube(Wood, V(0.4f, 1.85f, 0), V(0.9f, 0.3f, 0.05f))
            .Cube(Wood, V(-0.35f, 1.5f, 0), V(0.8f, 0.26f, 0.05f), V(0, 0, -6));
    }
}
