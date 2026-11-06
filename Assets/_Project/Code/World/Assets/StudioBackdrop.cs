using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// The backdrop of a preview card, as in a photo studio: a soft glow behind the subject fading to dark edges, and a
    /// soft shadow on the floor under it. Drawn once in code (no texture asset), so it scales with the card and costs
    /// nothing to ship. <see cref="Shade"/> is the picture; <see cref="Create"/> paints it into a texture.
    /// </summary>
    public static class StudioBackdrop
    {
        /// <summary>Width over height of the picture <see cref="Create"/> paints (and the card it is made for).</summary>
        public const float Aspect = 1.45f;

        /// <summary>Where the subject stands: the glow's centre, and the floor line its shadow lies on (0 bottom, 1 top).</summary>
        public static readonly Vector2 GlowCentre = new Vector2(0.5f, 0.56f);
        public const float FloorLine = 0.13f;

        static readonly Color Edge = new Color(0.075f, 0.08f, 0.09f);
        static readonly Color Glow = new Color(0.29f, 0.29f, 0.30f);
        const float GlowRadius = 0.62f;
        const float ShadowWidth = 0.17f;
        const float ShadowHeight = 0.045f;
        const float ShadowStrength = 0.6f;

        /// <summary>
        /// The colour at (<paramref name="u"/>, <paramref name="v"/>), both 0..1 across and up the picture. Distances are
        /// measured in the picture's height, so the glow and the shadow stay round on a wide card.
        /// </summary>
        public static Color Shade(float u, float v)
        {
            var x = (u - GlowCentre.x) * Aspect;
            var glowDistance = Mathf.Sqrt(x * x + (v - GlowCentre.y) * (v - GlowCentre.y)) / GlowRadius;
            var colour = Color.Lerp(Glow, Edge, Smooth(glowDistance));

            var sx = x / ShadowWidth;
            var sy = (v - FloorLine) / ShadowHeight;
            var shadow = 1f - Smooth(Mathf.Sqrt(sx * sx + sy * sy));
            colour = Color.Lerp(colour, Edge * 0.4f, shadow * ShadowStrength);
            colour.a = 1f;
            return colour;
        }

        /// <summary>A new texture <paramref name="height"/> pixels high (and <see cref="Aspect"/> times as wide) of the backdrop.</summary>
        public static Texture2D Create(int height)
        {
            var width = Mathf.RoundToInt(height * Aspect);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Studio backdrop",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = Shade((x + 0.5f) / width, (y + 0.5f) / height);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // 0 at the centre to 1 at distance 1 and beyond, easing in and out.
        static float Smooth(float distance) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance));
    }
}
