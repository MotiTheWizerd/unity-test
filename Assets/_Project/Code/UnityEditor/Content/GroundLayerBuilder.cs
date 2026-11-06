using System.IO;
using System.Linq;
using Forge.World.Assets;
using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Dev tool: turns each texture folder under Content/Ground into a terrain layer and lists it in the ground layer
    /// catalog under "ground/{folder name, lower case}". A folder holds a scan in ambientCG's naming (see
    /// <see cref="TextureMaps"/>); its maps are packed into the mask map Unity's terrain reads ({Name}_Mask.png:
    /// metallic, occlusion, height, smoothness = 1 − roughness). It also records each layer's map color, the color texture's average times the layer's tint, for
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
                catalog.SetMapColor(key, TextureMaps.AverageColor(TextureMaps.ColorPath(folder, name)) * (Color)layer.diffuseRemapMax);
                built++;
            }

            catalog.PreviewMaterial = BuildPreviewMaterial();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Forge] Built {built} ground layers; {CatalogPath} has {catalog.Count} entries.");
        }

        static TerrainLayer BuildLayer(string folder, string name)
        {
            var colorPath = TextureMaps.ColorPath(folder, name);
            var normalPath = TextureMaps.NormalPath(folder, name);
            if (!File.Exists(colorPath))
            {
                Debug.LogWarning($"[Forge] {folder} has no {name}_Color.jpg; skipped.");
                return null;
            }

            TextureMaps.Configure(colorPath, TextureImporterType.Default, sRGB: true);
            var hasNormal = File.Exists(normalPath);
            if (hasNormal)
                TextureMaps.Configure(normalPath, TextureImporterType.NormalMap, sRGB: false);
            var maskPath = TextureMaps.PackMask(folder, name);

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
    }
}
