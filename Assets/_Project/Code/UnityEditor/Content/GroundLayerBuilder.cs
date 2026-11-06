using System.IO;
using System.Linq;
using Forge.World.Assets;
using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Dev tool: turns each texture folder under Content/Ground into a terrain layer and lists it in the ground layer
    /// catalog under "ground/{folder name, lower case}". A folder holds {Name}_Color, {Name}_NormalGL and, optionally,
    /// {Name}_AmbientOcclusion, {Name}_Displacement and {Name}_Roughness (ambientCG's naming); the last three are packed
    /// into the mask map Unity's terrain reads ({Name}_Mask.png: metallic 0, occlusion, height, smoothness =
    /// 1 − roughness). It also records each layer's map color, the color texture's average times the layer's tint, for
    /// the far land. Existing assets are updated in place, so their GUIDs and hand-set tints survive a rebuild.
    /// </summary>
    static class GroundLayerBuilder
    {
        const string GroundFolder = "Assets/_Project/Content/Ground";
        public const string CatalogPath = "Assets/_Project/Content/GroundLayerCatalog.asset";
        const string PreviewMaterialPath = GroundFolder + "/GroundPreview.mat";

        /// <summary>Meters one repeat of a ground texture covers: the scans are of about a meter or two of ground.</summary>
        const float TileSize = 3f;

        /// <summary>Most smoothness a ground layer shows (its mask map's smoothness is scaled into [0, this]).</summary>
        const float MaxSmoothness = 0.35f;

        [MenuItem("Forge/Ground/Build Ground Layers")]
        static void Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GroundLayerCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GroundLayerCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var built = 0;
            foreach (var folder in AssetDatabase.GetSubFolders(GroundFolder).OrderBy(f => f, System.StringComparer.Ordinal))
            {
                var name = Path.GetFileName(folder);
                var layer = BuildLayer(folder, name);
                if (layer == null)
                    continue;
                var key = "ground/" + name.ToLowerInvariant();
                catalog.Set(key, layer);
                // The layer's tint (its diffuse remap, set by hand on the asset) colors the far land too.
                catalog.SetMapColor(key, AverageColor($"{folder}/{name}_Color.jpg") * (Color)layer.diffuseRemapMax);
                built++;
            }

            catalog.PreviewMaterial = BuildPreviewMaterial();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Forge] Built {built} ground layers; {CatalogPath} has {catalog.Count} entries.");
        }

        static TerrainLayer BuildLayer(string folder, string name)
        {
            var colorPath = $"{folder}/{name}_Color.jpg";
            var normalPath = $"{folder}/{name}_NormalGL.jpg";
            if (!File.Exists(colorPath))
            {
                Debug.LogWarning($"[Forge] {folder} has no {name}_Color.jpg; skipped.");
                return null;
            }

            Configure(colorPath, TextureImporterType.Default, sRGB: true);
            var hasNormal = File.Exists(normalPath);
            if (hasNormal)
                Configure(normalPath, TextureImporterType.NormalMap, sRGB: false);
            var maskPath = PackMask(folder, name);
            Configure(maskPath, TextureImporterType.Default, sRGB: false);

            var layerPath = $"{folder}/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            layer.normalMapTexture = hasNormal ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null;
            layer.normalScale = 1f;
            layer.maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            layer.tileSize = new Vector2(TileSize, TileSize);
            layer.metallic = 0f;
            // Natural ground is matte: scans' smoothness, taken whole, gives grass a wet sun glare.
            layer.maskMapRemapMax = new Vector4(1f, 1f, 1f, MaxSmoothness);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        // The template ground material balls are copied from: URP Lit with the maps a layer fills switched on. As an
        // asset the catalog points at, its shader variants are kept in a build.
        static Material BuildPreviewMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PreviewMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, PreviewMaterialPath);
            }

            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_Smoothness", 1f); // scales the mask map's smoothness, which is the real value
            EditorUtility.SetDirty(material);
            return material;
        }

        // Metallic 0, occlusion, height and smoothness in R, G, B, A; a missing map gives its neutral value.
        static string PackMask(string folder, string name)
        {
            var occlusion = Load($"{folder}/{name}_AmbientOcclusion.jpg");
            var height = Load($"{folder}/{name}_Displacement.jpg");
            var roughness = Load($"{folder}/{name}_Roughness.jpg");
            var size = Load($"{folder}/{name}_Color.jpg");
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

                pixels[y * width + x] = new Color32(0, Sample(occlusion, 255), Sample(height, 128), (byte)(255 - Sample(roughness, 128)));
            }

            mask.SetPixels32(pixels);
            var path = $"{folder}/{name}_Mask.png";
            File.WriteAllBytes(path, mask.EncodeToPNG());
            Object.DestroyImmediate(mask);
            foreach (var map in new[] { occlusion, height, roughness }.Where(m => m != null))
                Object.DestroyImmediate(map);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        // The color texture's average, linear: what the ground looks like from afar.
        static Color AverageColor(string path)
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

        // Decodes an image file straight from disk (readable, unlike an imported texture); null when there is none.
        static Texture2D Load(string path)
        {
            if (!File.Exists(path))
                return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear: true);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        static void Configure(string path, TextureImporterType type, bool sRGB)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                AssetDatabase.ImportAsset(path);
                importer = (TextureImporter)AssetImporter.GetAtPath(path);
            }

            if (importer.textureType == type && importer.sRGBTexture == sRGB && importer.maxTextureSize == 1024)
                return;
            importer.textureType = type;
            importer.sRGBTexture = sRGB;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}
