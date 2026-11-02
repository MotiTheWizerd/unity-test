using System.Collections.Generic;
using Forge.World.Assets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Dev tool: (re)builds the placeholder materials and prefabs from <see cref="PlaceholderContent"/>
    /// and registers them in the prefab catalog. Existing assets are updated in place, so their
    /// GUIDs, and every reference to them, survive a regeneration. Catalog entries for other
    /// keys are left alone.
    /// </summary>
    static class PlaceholderContentGenerator
    {
        const string ContentFolder = "Assets/_Project/Content";
        const string PlaceholderFolder = ContentFolder + "/Placeholder";
        const string MaterialsFolder = PlaceholderFolder + "/Materials";
        const string PrefabsFolder = PlaceholderFolder + "/Prefabs";
        public const string CatalogPath = ContentFolder + "/PrefabCatalog.asset";

        const string LitShader = "Universal Render Pipeline/Lit";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [MenuItem("Forge/Generate Placeholder Content")]
        static void Generate()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);

            var materials = BuildMaterials();
            var catalog = LoadOrCreateCatalog();

            // Build in a preview scene, so the open scene is never touched.
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var definition in PlaceholderContent.All)
                    catalog.Set(definition.AssetKey, BuildPrefab(definition, materials, scene));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Forge] Generated {PlaceholderContent.All.Count} placeholder prefabs and {materials.Count} materials; {CatalogPath} has {catalog.Count} entries.");
        }

        static Dictionary<string, Material> BuildMaterials()
        {
            var shader = Shader.Find(LitShader);
            if (shader == null)
                throw new System.InvalidOperationException($"Shader '{LitShader}' not found; is URP installed?");

            var materials = new Dictionary<string, Material>();
            foreach (var spec in PlaceholderMaterials.All)
            {
                var path = $"{MaterialsFolder}/{spec.Name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = shader;
                material.SetColor(BaseColorId, spec.Color);
                material.SetFloat(MetallicId, spec.Metallic);
                material.SetFloat(SmoothnessId, spec.Smoothness);
                if (spec.Emission.HasValue)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor(EmissionColorId, spec.Emission.Value);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                    material.SetColor(EmissionColorId, Color.black);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }

                EditorUtility.SetDirty(material);
                materials.Add(spec.Name, material);
            }

            return materials;
        }

        static GameObject BuildPrefab(ContentDefinition definition, IReadOnlyDictionary<string, Material> materials, Scene scene)
        {
            var root = new GameObject(definition.Name);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                definition.Build(new PrefabBuilder(root, materials));
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsFolder}/{definition.Name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static PrefabCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PrefabCatalog>(CatalogPath);
            if (catalog != null)
                return catalog;

            catalog = ScriptableObject.CreateInstance<PrefabCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
