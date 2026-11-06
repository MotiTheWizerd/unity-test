using System.IO;
using System.Linq;
using Forge.World.Assets;
using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Dev tool: turns each texture folder under Content/Materials into a URP Lit material ({Name}.mat) and lists it in
    /// the material catalog under "material/{folder name, lower case}". A folder holds a scan in ambientCG's naming (see
    /// <see cref="TextureMaps"/>); its metalness, occlusion and roughness are packed into one mask the material reads
    /// as its metallic-smoothness map (R and A) and occlusion map (G). Existing materials are updated in place, so their
    /// GUIDs and anything set on them by hand (tint, tiling) survive a rebuild.
    /// </summary>
    static class MaterialBuilder
    {
        const string MaterialsFolder = "Assets/_Project/Content/Materials";
        public const string CatalogPath = "Assets/_Project/Content/MaterialCatalog.asset";

        /// <summary>Asset key of the material built from <paramref name="folderName"/>.</summary>
        public static string KeyFor(string folderName) => "material/" + folderName.ToLowerInvariant();

        [MenuItem("Forge/Materials/Build Materials")]
        static void Build()
        {
            var catalog = LoadOrCreateCatalog(CatalogPath);
            var built = BuildFolders(MaterialsFolder, catalog, KeyFor);
            Debug.Log($"[Forge] Built {built} materials; {CatalogPath} has {catalog.Count} entries.");
        }

        /// <summary>The catalog at <paramref name="path"/>, made there if there is none yet.</summary>
        public static MaterialCatalog LoadOrCreateCatalog(string path)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MaterialCatalog>(path);
            if (catalog != null)
                return catalog;

            catalog = ScriptableObject.CreateInstance<MaterialCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
            return catalog;
        }

        /// <summary>
        /// Builds a material from each texture folder directly under <paramref name="parent"/> and lists it in
        /// <paramref name="catalog"/> under the key <paramref name="keyFor"/> gives its folder name. Returns how many were built.
        /// </summary>
        public static int BuildFolders(string parent, MaterialCatalog catalog, System.Func<string, string> keyFor)
        {
            var built = 0;
            foreach (var folder in AssetDatabase.GetSubFolders(parent).OrderBy(f => f, System.StringComparer.Ordinal))
            {
                var name = Path.GetFileName(folder);
                var material = BuildMaterial(folder, name);
                if (material == null)
                    continue;
                catalog.Set(keyFor(name), material);
                built++;
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return built;
        }

        static Material BuildMaterial(string folder, string name)
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
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureMaps.PackMask(folder, name));

            var path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath));
            material.SetTexture("_BumpMap", hasNormal ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null);
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetTexture("_OcclusionMap", mask);
            material.SetFloat("_Smoothness", 1f); // scales the mask's smoothness, which is the real value
            material.SetFloat("_OcclusionStrength", 1f);
            SetKeyword(material, "_NORMALMAP", hasNormal);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", true);
            SetKeyword(material, "_OCCLUSIONMAP", true);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
    }
}
