using System.Collections.Generic;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>The flat-colored URP materials placeholder prefabs are made of.</summary>
    static class PlaceholderMaterials
    {
        public const string Bark = "Bark";
        public const string Leaves = "Leaves";
        public const string LeavesDark = "LeavesDark";
        public const string Stone = "Stone";
        public const string Wood = "Wood";
        public const string WoodDark = "WoodDark";
        public const string Plaster = "Plaster";
        public const string Roof = "Roof";
        public const string Metal = "Metal";
        public const string Window = "Window";
        public const string LampGlow = "LampGlow";
        public const string Fire = "Fire";
        public const string Tunic = "Tunic";
        public const string Leather = "Leather";
        public const string Skin = "Skin";

        public static IReadOnlyList<Spec> All { get; } = new[]
        {
            new Spec(Bark, new Color(0.36f, 0.25f, 0.17f)),
            new Spec(Leaves, new Color(0.30f, 0.52f, 0.20f)),
            new Spec(LeavesDark, new Color(0.14f, 0.34f, 0.17f)),
            new Spec(Stone, new Color(0.52f, 0.52f, 0.50f)),
            new Spec(Wood, new Color(0.55f, 0.38f, 0.22f)),
            new Spec(WoodDark, new Color(0.30f, 0.20f, 0.12f)),
            new Spec(Plaster, new Color(0.86f, 0.80f, 0.68f)),
            new Spec(Roof, new Color(0.62f, 0.22f, 0.16f)),
            new Spec(Metal, new Color(0.20f, 0.21f, 0.23f), metallic: 0.6f, smoothness: 0.5f),
            new Spec(Window, new Color(0.12f, 0.16f, 0.22f), smoothness: 0.85f),
            new Spec(LampGlow, new Color(1f, 0.85f, 0.5f), emission: new Color(1f, 0.8f, 0.45f) * 2f),
            new Spec(Fire, new Color(1f, 0.45f, 0.1f), emission: new Color(1f, 0.4f, 0.08f) * 3f),
            new Spec(Tunic, new Color(0.24f, 0.32f, 0.46f)),
            new Spec(Leather, new Color(0.27f, 0.19f, 0.13f), smoothness: 0.35f),
            new Spec(Skin, new Color(0.78f, 0.60f, 0.48f), smoothness: 0.3f),
        };

        public readonly struct Spec
        {
            public Spec(string name, Color color, float metallic = 0f, float smoothness = 0.2f, Color? emission = null)
            {
                Name = name;
                Color = color;
                Metallic = metallic;
                Smoothness = smoothness;
                Emission = emission;
            }

            public string Name { get; }

            public Color Color { get; }

            public float Metallic { get; }

            public float Smoothness { get; }

            public Color? Emission { get; }
        }
    }
}
