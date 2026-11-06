using System.IO;
using System.Linq;
using Forge.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Dev tool: turns each model folder under Content/Models into a placeable asset, as the character builder does for
    /// characters. A folder {Name} holds the model {Name}.fbx (imported by <see cref="ModelImport"/>; meters) and its
    /// textures in the art repo's naming, each optional: {Name}_BaseColor.png, {Name}_MetallicSmoothness.png (metal in R,
    /// smoothness in A) and {Name}_Normal.png (OpenGL). The tool makes a URP Lit material {Name}.mat from them and a
    /// prefab {Name}.prefab holding the model as it rests in the world: turned as its optional {Name}.json says
    /// (<c>{ "rest": [90, 0, 0] }</c>: a sword modelled point up, as a hand holds it, lies flat with its point to the
    /// front), with its lowest point on the prefab's pivot and its footprint centred on it, as every asset's pivot is. The
    /// model inside keeps its own pivot (a weapon's grip) for whatever holds it later. The prefab is listed in the prefab
    /// catalog under <see cref="ModelImport.KeyFor"/>. Existing assets are updated in place, so their GUIDs, and every
    /// reference to them, survive a rebuild.
    /// </summary>
    static class ModelBuilder
    {
        const int TextureSize = 2048;

        [MenuItem("Forge/Models/Build Models")]
        static void Build()
        {
            var catalog = PlaceholderContentGenerator.LoadOrCreateCatalog();
            var built = 0;
            foreach (var folder in AssetDatabase.GetSubFolders(ModelImport.ModelsFolder).OrderBy(f => f, System.StringComparer.Ordinal))
            {
                var name = Path.GetFileName(folder);
                var prefab = BuildModel(folder, name);
                if (prefab == null)
                    continue;
                catalog.Set(ModelImport.KeyFor(name), prefab);
                built++;
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Forge] Built {built} model(s) under {ModelImport.ModelsFolder}; {PlaceholderContentGenerator.CatalogPath} has {catalog.Count} entries.");
        }

        /// <summary>The prefab built for the model folder <paramref name="folder"/>, or null when it holds no model.</summary>
        public static GameObject BuildModel(string folder, string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.fbx");
            if (model == null)
            {
                Debug.LogWarning($"[Forge] {folder} has no {name}.fbx; skipped.");
                return null;
            }

            var material = BuildMaterial(folder, name);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, scene);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                instance.transform.SetParent(root.transform, false);
                instance.transform.localRotation = Quaternion.Euler(RestOf(folder, name));
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }

                // The asset contract: the pivot is the ground contact point, the bottom centre of what it draws.
                if (RendererBounds.TryGetLocal(root, out var bounds))
                    instance.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                return PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{name}.prefab");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // How the model rests in the world, in degrees (Unity order: Z, then X, then Y): its optional {Name}.json's "rest".
        static Vector3 RestOf(string folder, string name)
        {
            var path = $"{folder}/{name}.json";
            if (!File.Exists(path))
                return Vector3.zero;

            var rest = JsonUtility.FromJson<ModelSettings>(File.ReadAllText(path))?.rest;
            if (rest == null || rest.Length == 0)
                return Vector3.zero;
            if (rest.Length != 3)
                throw new System.InvalidOperationException($"{path}: \"rest\" is three angles in degrees, [x, y, z].");
            return new Vector3(rest[0], rest[1], rest[2]);
        }

        // A model folder's optional {Name}.json.
        [System.Serializable]
        sealed class ModelSettings
        {
            public float[] rest;
        }

        static Material BuildMaterial(string folder, string name)
        {
            var colorPath = $"{folder}/{name}_BaseColor.png";
            var metalPath = $"{folder}/{name}_MetallicSmoothness.png";
            var normalPath = $"{folder}/{name}_Normal.png";
            var hasColor = File.Exists(colorPath);
            var hasMetal = File.Exists(metalPath);
            var hasNormal = File.Exists(normalPath);
            if (hasColor)
                TextureMaps.Configure(colorPath, TextureImporterType.Default, sRGB: true, TextureSize);
            if (hasMetal)
                TextureMaps.Configure(metalPath, TextureImporterType.Default, sRGB: false, TextureSize);
            if (hasNormal)
                TextureMaps.Configure(normalPath, TextureImporterType.NormalMap, sRGB: false, TextureSize);

            var path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", hasColor ? AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath) : null);
            material.SetTexture("_MetallicGlossMap", hasMetal ? AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath) : null);
            material.SetTexture("_BumpMap", hasNormal ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null);
            // With the map, smoothness is its alpha (scaled by this); without, a plain matte surface.
            material.SetFloat("_Smoothness", hasMetal ? 1f : 0.25f);
            material.SetFloat("_Metallic", 0f);
            MaterialBuilder.SetKeyword(material, "_METALLICSPECGLOSSMAP", hasMetal);
            MaterialBuilder.SetKeyword(material, "_NORMALMAP", hasNormal);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
