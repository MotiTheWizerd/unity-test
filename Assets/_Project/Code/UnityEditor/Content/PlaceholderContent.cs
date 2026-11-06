using System.Collections.Generic;
using Forge.Data.Records;
using UnityEngine;
using static Forge.UnityEditor.Content.PlaceholderMaterials;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The base game's placeable objects with placeholder art built from primitives. Real art
    /// replaces a placeholder by pointing its asset key at another prefab in the catalog;
    /// the records, and every object placed from them, stay as they are. Like any asset, an
    /// object's front faces local +Z (Unity's forward): a house's door, a bench's seat.
    /// </summary>
    static class PlaceholderContent
    {
        public static IReadOnlyList<ContentDefinition> All { get; } = new[]
        {
            // Trees grow straight up whatever the slope; low ground cover and built runs hug it.
            new ContentDefinition("tree_pine", "Pine Tree", "Nature/Trees", "nature/tree_pine", PineTree, Natural(0.75f, 1.35f, alignToSlope: false)),
            new ContentDefinition("tree_oak", "Oak Tree", "Nature/Trees", "nature/tree_oak", OakTree, Natural(0.8f, 1.3f, alignToSlope: false)),
            new ContentDefinition("bush", "Bush", "Nature/Plants", "nature/bush", Bush, Natural(0.7f, 1.3f, alignToSlope: true)),
            new ContentDefinition("rock_cluster", "Rock Cluster", "Nature/Rocks", "nature/rock_cluster", RockCluster, Natural(0.6f, 1.5f, alignToSlope: true)),
            new ContentDefinition("house_small", "Small House", "Architecture/Buildings", "architecture/house_small", SmallHouse),
            new ContentDefinition("wall_stone", "Stone Wall", "Architecture/Walls", "architecture/wall_stone", StoneWall, FollowsSlope),
            new ContentDefinition("fence_wood", "Wooden Fence", "Architecture/Walls", "architecture/fence_wood", WoodenFence, FollowsSlope),
            // Interior pieces on a 4 m grid: floors tile edge to edge, walls stand on a floor's edge.
            new ContentDefinition("floor_wood", "Wooden Floor", "Architecture/Interior", "architecture/floor_wood", WoodenFloor),
            new ContentDefinition("wall_interior", "Interior Wall", "Architecture/Interior", "architecture/wall_interior", InteriorWall),
            new ContentDefinition("lamp_post", "Lamp Post", "Props/Lighting", "props/lamp_post", LampPost),
            new ContentDefinition("campfire", "Campfire", "Props/Lighting", "props/campfire", Campfire, FollowsSlope),
            new ContentDefinition("barrel", "Barrel", "Props/Containers", "props/barrel", Barrel),
            new ContentDefinition("bench", "Bench", "Props/Furniture", "props/bench", Bench),
            new ContentDefinition("signpost", "Signpost", "Props/Decor", "props/signpost", Signpost),
            // Items, to pick up and carry; filed under Items, so the asset browser gives them their own tab.
            new ContentDefinition("potion_healing", "Healing Potion", "Items/Potions", "items/potion_healing", HealingPotion, Loose,
                new ItemPart(ItemKind.Potion, weight: 0.5f, value: 15)),
            new ContentDefinition("apple_red", "Red Apple", "Items/Food", "items/apple_red", RedApple, Loose,
                new ItemPart(ItemKind.Food, weight: 0.1f, value: 3)),
            new ContentDefinition("gold", "Gold", "Items/Misc", "items/gold", GoldCoins, Loose,
                new ItemPart(ItemKind.Misc, weight: 0f, value: 1)),
        };

        /// <summary>Natural things: any heading, a size within [min, max].</summary>
        public static PlacementRules Natural(float minScale, float maxScale, bool alignToSlope) =>
            new PlacementRules(true, minScale, maxScale, alignToSlope);

        /// <summary>Placed exactly as aimed, but tilted to the ground (fences, walls, a campfire).</summary>
        // Computed, not a static field: `All` above is initialized first and would see a null field.
        public static PlacementRules FollowsSlope => new PlacementRules(false, 1f, 1f, true);

        /// <summary>Items lie any way round, upright, at their real size (what one weighs is one of them).</summary>
        public static PlacementRules Loose => new PlacementRules(true, 1f, 1f, false);

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

        // Walls 4 × 3 × 5 m; the roof is a cube turned 45° so its upper half forms a gable. Door and windows in front (+Z).
        static void SmallHouse(PrefabBuilder b) => b
            .Cube(Plaster, V(0, 1.5f, 0), V(4f, 3f, 5f))
            .Cube(Roof, V(0, 3f, 0), V(3.11f, 3.11f, 5.4f), V(0, 0, 45))
            .Cube(WoodDark, V(0, 0.9f, 2.51f), V(0.9f, 1.8f, 0.05f))
            .Cube(Window, V(1.25f, 1.8f, 2.51f), V(0.7f, 0.6f, 0.05f))
            .Cube(Window, V(-1.25f, 1.8f, 2.51f), V(0.7f, 0.6f, 0.05f))
            .Cube(Stone, V(1.2f, 4.4f, -1.3f), V(0.5f, 1.6f, 0.5f));

        static void StoneWall(PrefabBuilder b) => b
            .Cube(Stone, V(0, 1f, 0), V(4f, 2f, 0.6f))
            .Cube(Stone, V(0, 2.05f, 0), V(4.2f, 0.15f, 0.75f));

        static void WoodenFence(PrefabBuilder b) => b
            .Cube(Wood, V(-1.5f, 0.6f, 0), V(0.15f, 1.2f, 0.15f))
            .Cube(Wood, V(1.5f, 0.6f, 0), V(0.15f, 1.2f, 0.15f))
            .Cube(Wood, V(0, 0.9f, 0), V(3.1f, 0.1f, 0.06f))
            .Cube(Wood, V(0, 0.45f, 0), V(3.1f, 0.1f, 0.06f));

        // 4 × 4 m of planks, 0.2 m thick, with the boards running front to back.
        static void WoodenFloor(PrefabBuilder b)
        {
            b.Cube(WoodDark, V(0, 0.08f, 0), V(4f, 0.16f, 4f));
            for (var i = 0; i < 8; i++)
                b.Cube(i % 2 == 0 ? Wood : WoodDark, V(-1.75f + i * 0.5f, 0.18f, 0), V(0.48f, 0.04f, 4f));
        }

        // 4 m long, 3 m high, 0.25 m thick, with a skirting board along the front.
        static void InteriorWall(PrefabBuilder b) => b
            .Cube(Plaster, V(0, 1.5f, 0), V(4f, 3f, 0.25f))
            .Cube(WoodDark, V(0, 0.08f, 0.14f), V(4f, 0.16f, 0.04f));

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

        // The backrest is behind the seat, so whoever sits faces the front (+Z).
        static void Bench(PrefabBuilder b) => b
            .Cube(Wood, V(0, 0.45f, 0), V(1.6f, 0.08f, 0.45f))
            .Cube(Wood, V(0, 0.8f, -0.2f), V(1.6f, 0.4f, 0.06f))
            .Cube(WoodDark, V(0.7f, 0.21f, 0), V(0.08f, 0.42f, 0.4f))
            .Cube(WoodDark, V(-0.7f, 0.21f, 0), V(0.08f, 0.42f, 0.4f));

        static void Signpost(PrefabBuilder b) => b
            .Post(Wood, Vector3.zero, 0.1f, 2.2f)
            .Cube(Wood, V(0.4f, 1.85f, 0), V(0.9f, 0.3f, 0.05f))
            .Cube(Wood, V(-0.35f, 1.5f, 0), V(0.8f, 0.26f, 0.05f), V(0, 0, -6));

        // A round flask 13 cm tall: body, neck and cork.
        static void HealingPotion(PrefabBuilder b) => b
            .Sphere(PotionRed, V(0, 0.045f, 0), V(0.08f, 0.09f, 0.08f))
            .Post(PotionRed, V(0, 0.08f, 0), 0.025f, 0.035f)
            .Post(Wood, V(0, 0.112f, 0), 0.03f, 0.02f);

        // 8 cm across, with a stem and a leaf too thin to be worth colliders.
        static void RedApple(PrefabBuilder b) => b
            .Sphere(AppleRed, V(0, 0.0375f, 0), V(0.08f, 0.075f, 0.08f))
            .Post(WoodDark, V(0, 0.07f, 0), 0.006f, 0.02f, solid: false)
            .Sphere(Leaves, V(0.012f, 0.085f, 0), V(0.025f, 0.004f, 0.012f), V(0, 0, -20), solid: false);

        // A stack of four coins and three strewn round it, 8 cm across. A coin's own collider would be a ball, so the
        // coins are hollow and one box covers the pile.
        static void GoldCoins(PrefabBuilder b)
        {
            const float diameter = 0.026f, thickness = 0.003f;
            for (var i = 0; i < 4; i++)
                b.Post(Gold, V(0.001f * i, thickness * i, -0.001f * i), diameter, thickness, solid: false);
            b.Post(Gold, V(0.03f, 0, 0.012f), diameter, thickness, solid: false)
                .Post(Gold, V(-0.026f, 0, 0.016f), diameter, thickness, solid: false)
                .Post(Gold, V(0.006f, 0, -0.03f), diameter, thickness, solid: false)
                .Box(V(0.002f, 0.006f, -0.007f), V(0.082f, 0.012f, 0.072f));
        }
    }
}
