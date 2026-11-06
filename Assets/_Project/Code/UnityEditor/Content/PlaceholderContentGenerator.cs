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
    /// and registers them in the prefab catalog, and the player's mannequin (<see cref="PlaceholderCharacters"/>), which
    /// the player profile wears unless it has a model of its own. Existing assets are updated in place, so their
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
        const string PlayerProfilePath = "Assets/_Project/Settings/Player/PlayerProfile.asset";
        const string PlayerModelField = "_model";

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
                    catalog.Set(definition.AssetKey, BuildPrefab(definition.Name, definition.Build, materials, scene, keepColliders: true));
                DressPlayer(BuildMannequin(materials, scene));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Forge] Generated {PlaceholderContent.All.Count} placeholder prefabs, the player's mannequin and {materials.Count} materials; {CatalogPath} has {catalog.Count} entries.");
        }

        /// <summary>Only the player's mannequin (and the materials), leaving the object prefabs as they are.</summary>
        [MenuItem("Forge/Generate Placeholder Player")]
        static void GeneratePlayer()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);

            var materials = BuildMaterials();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                DressPlayer(BuildMannequin(materials, scene));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Forge] Generated the player's mannequin ({PrefabsFolder}/{PlaceholderCharacters.PlayerMannequin}.prefab).");
        }

        // The player's body is its own collider, so its look has none.
        static GameObject BuildMannequin(IReadOnlyDictionary<string, Material> materials, Scene scene) =>
            BuildPrefab(PlaceholderCharacters.PlayerMannequin, PlaceholderCharacters.Mannequin, materials, scene, keepColliders: false);

        // The player profile wears the mannequin until it is given a model of its own.
        static void DressPlayer(GameObject mannequin)
        {
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PlayerProfilePath);
            if (profile == null)
            {
                Debug.LogWarning($"[Forge] No player profile at {PlayerProfilePath}; the mannequin is built but nobody wears it.");
                return;
            }

            var model = new SerializedObject(profile).FindProperty(PlayerModelField)
                        ?? throw new System.InvalidOperationException($"The player profile has no '{PlayerModelField}' field.");
            if (model.objectReferenceValue != null)
                return;
            model.objectReferenceValue = mannequin;
            model.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
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

        static GameObject BuildPrefab(string name, System.Action<PrefabBuilder> build, IReadOnlyDictionary<string, Material> materials, Scene scene, bool keepColliders)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                build(new PrefabBuilder(root, materials));
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                if (!keepColliders)
                {
                    foreach (var collider in root.GetComponentsInChildren<Collider>())
                        Object.DestroyImmediate(collider);
                }

                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsFolder}/{name}.prefab");
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
