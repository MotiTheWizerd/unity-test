using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The texture work the content builders share. A texture folder holds a scanned surface in ambientCG's naming:
    /// {Name}_Color, {Name}_NormalGL and, optionally, {Name}_AmbientOcclusion, {Name}_Displacement, {Name}_Roughness and
    /// {Name}_Metalness, all JPG.
    /// </summary>
    static class TextureMaps
    {
        public static string ColorPath(string folder, string name) => $"{folder}/{name}_Color.jpg";

        public static string NormalPath(string folder, string name) => $"{folder}/{name}_NormalGL.jpg";

        /// <summary>
        /// Packs the folder's metalness, occlusion, height and smoothness (1 − roughness) into R, G, B, A of
        /// {Name}_Mask.png, the mask both a terrain layer and URP Lit (metallic-smoothness and occlusion maps) read; a
        /// missing map gives its neutral value (metal 0, no occlusion, mid height, mid roughness). Imported linear, at
        /// most <paramref name="maxSize"/> px.
        /// </summary>
        public static string PackMask(string folder, string name, int maxSize = 1024)
        {
            var metalness = Load($"{folder}/{name}_Metalness.jpg");
            var occlusion = Load($"{folder}/{name}_AmbientOcclusion.jpg");
            var height = Load($"{folder}/{name}_Displacement.jpg");
            var roughness = Load($"{folder}/{name}_Roughness.jpg");
            var size = Load(ColorPath(folder, name));
            var width = size.width;
            var heightPx = size.height;
            Object.DestroyImmediate(size);

            var mask = new Texture2D(width, heightPx, TextureFormat.RGBA32, false, linear: true);
            var pixels = new Color32[width * heightPx];
            for (var y = 0; y < heightPx; y++)
            for (var x = 0; x < width; x++)
            {
                byte Sample(Texture2D map, byte neutral) =>
                    map == null ? neutral : (byte)(map.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / heightPx).r * 255f + 0.5f);

                pixels[y * width + x] = new Color32(Sample(metalness, 0), Sample(occlusion, 255), Sample(height, 128), (byte)(255 - Sample(roughness, 128)));
            }

            mask.SetPixels32(pixels);
            var path = $"{folder}/{name}_Mask.png";
            File.WriteAllBytes(path, mask.EncodeToPNG());
            Object.DestroyImmediate(mask);
            foreach (var map in new[] { metalness, occlusion, height, roughness }.Where(m => m != null))
                Object.DestroyImmediate(map);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Configure(path, TextureImporterType.Default, sRGB: false, maxSize);
            return path;
        }

        /// <summary>The color texture's average, linear: what the surface looks like from afar.</summary>
        public static Color AverageColor(string path)
        {
            var texture = Load(path);
            var pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);

            double r = 0, g = 0, b = 0;
            foreach (var pixel in pixels)
            {
                // Averaged as light (linear), not as sRGB numbers, so the result is the color the eye blends to.
                r += Mathf.GammaToLinearSpace(pixel.r / 255f);
                g += Mathf.GammaToLinearSpace(pixel.g / 255f);
                b += Mathf.GammaToLinearSpace(pixel.b / 255f);
            }

            return new Color((float)(r / pixels.Length), (float)(g / pixels.Length), (float)(b / pixels.Length), 1f);
        }

        /// <summary>Sets how a texture imports (at most <paramref name="maxSize"/> px), reimporting only when that changes.</summary>
        public static void Configure(string path, TextureImporterType type, bool sRGB, int maxSize = 1024)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                AssetDatabase.ImportAsset(path);
                importer = (TextureImporter)AssetImporter.GetAtPath(path);
            }

            if (importer.textureType == type && importer.sRGBTexture == sRGB && importer.maxTextureSize == maxSize)
                return;
            importer.textureType = type;
            importer.sRGBTexture = sRGB;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }

        // Decodes an image file straight from disk (readable, unlike an imported texture); null when there is none.
        static Texture2D Load(string path)
        {
            if (!File.Exists(path))
                return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear: true);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }
    }
}
