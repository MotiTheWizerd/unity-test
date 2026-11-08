using System.IO;
using System.Linq;
using Forge.Play;
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
    /// smoothness in A), {Name}_Normal.png (OpenGL) and {Name}_Occlusion.png (ambient occlusion). The tool makes a URP Lit material {Name}.mat from them and a
    /// prefab {Name}.prefab holding the model as it rests in the world: turned as its optional {Name}.json says
    /// (<c>{ "rest": [90, 0, 0] }</c>: a sword modelled point up, as a hand holds it, lies flat with its point to the
    /// front), with its lowest point on the prefab's pivot and its footprint centred on it, as every asset's pivot is. The
    /// model inside keeps its own pivot (a weapon's grip) for whatever holds it later, and, when the json has a "leftGrip"
    /// (<c>[0, 180, 0]</c>: a bow, whose string must face the archer in either hand), a left-hand grip turned by it
    /// (<see cref="ModelGrip"/>). The prefab is listed in the prefab
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
                var settings = SettingsOf(folder, name);
                instance.transform.localRotation = Quaternion.Euler(settings.Resting);
                if (settings.LeftGrip.HasValue)
                {
                    var leftGrip = new GameObject(ModelGrip.LeftHand).transform;
                    leftGrip.SetParent(instance.transform, false);
                    leftGrip.localRotation = Quaternion.Euler(settings.LeftGrip.Value);
                }

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

        // A model folder's optional {Name}.json, angles in degrees (Unity order: Z, then X, then Y): how the model rests in
        // the world ("rest"), and the turn the left hand holds it by, about its grip ("leftGrip": a bow half round its
        // length, so its string faces the archer in either hand); none when it is held alike in both.
        static (Vector3 Resting, Vector3? LeftGrip) SettingsOf(string folder, string name)
        {
            var path = $"{folder}/{name}.json";
            if (!File.Exists(path))
                return (Vector3.zero, null);

            var settings = JsonUtility.FromJson<ModelSettings>(File.ReadAllText(path));
            return (Angles(path, "rest", settings?.rest) ?? Vector3.zero, Angles(path, "leftGrip", settings?.leftGrip));
        }

        static Vector3? Angles(string path, string key, float[] angles)
        {
            if (angles == null || angles.Length == 0)
                return null;
            if (angles.Length != 3)
                throw new System.InvalidOperationException($"{path}: \"{key}\" is three angles in degrees, [x, y, z].");
            return new Vector3(angles[0], angles[1], angles[2]);
        }

        [System.Serializable]
        sealed class ModelSettings
        {
            public float[] rest;
            public float[] leftGrip;
        }

        static Material BuildMaterial(string folder, string name)
        {
            var colorPath = $"{folder}/{name}_BaseColor.png";
            var metalPath = $"{folder}/{name}_MetallicSmoothness.png";
            var normalPath = $"{folder}/{name}_Normal.png";
            var occlusionPath = $"{folder}/{name}_Occlusion.png";
            var hasColor = File.Exists(colorPath);
            var hasMetal = File.Exists(metalPath);
            var hasNormal = File.Exists(normalPath);
            var hasOcclusion = File.Exists(occlusionPath);
            if (hasColor)
                TextureMaps.Configure(colorPath, TextureImporterType.Default, sRGB: true, TextureSize);
            if (hasMetal)
                TextureMaps.Configure(metalPath, TextureImporterType.Default, sRGB: false, TextureSize);
            if (hasNormal)
                TextureMaps.Configure(normalPath, TextureImporterType.NormalMap, sRGB: false, TextureSize);
            if (hasOcclusion)
                TextureMaps.Configure(occlusionPath, TextureImporterType.Default, sRGB: false, TextureSize);

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
            material.SetTexture("_OcclusionMap", hasOcclusion ? AssetDatabase.LoadAssetAtPath<Texture2D>(occlusionPath) : null);
            // With the map, smoothness is its alpha (scaled by this); without, a plain matte surface.
            material.SetFloat("_Smoothness", hasMetal ? 1f : 0.25f);
            material.SetFloat("_Metallic", 0f);
            MaterialBuilder.SetKeyword(material, "_METALLICSPECGLOSSMAP", hasMetal);
            MaterialBuilder.SetKeyword(material, "_NORMALMAP", hasNormal);
            MaterialBuilder.SetKeyword(material, "_OCCLUSIONMAP", hasOcclusion);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
