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
    /// smoothness in A), {Name}_Normal.png (OpenGL) and {Name}_Occlusion.png (ambient occlusion); a cutout model's
    /// {Name}_BaseColor.png carries its alpha. The tool makes a URP Lit material {Name}.mat from them and a
    /// prefab {Name}.prefab holding the model as it rests in the world: turned as its optional {Name}.json says
    /// (<c>{ "rest": [90, 0, 0] }</c>: a sword modelled point up, as a hand holds it, lies flat with its point to the
    /// front), with its lowest point on the prefab's pivot and its footprint centred on it, as every asset's pivot is. The
    /// model inside keeps its own pivot (a weapon's grip) for whatever holds it later, and, when the json has a
    /// "facesHolder" (<c>[-1, 0, 0]</c>: a bow, whose string must face the archer) or a "carried" (<c>"back"</c>), a
    /// <see cref="HeldLook"/> saying so. The prefab is listed in the prefab
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

            var settings = SettingsOf(folder, name);
            var material = BuildMaterial(folder, name, settings.Cutout);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, scene);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                instance.transform.SetParent(root.transform, false);
                instance.transform.localRotation = Quaternion.Euler(settings.Resting);
                if (settings.FacesHolder.HasValue || settings.Carried != CarriedAt.Hip)
                    root.AddComponent<HeldLook>().Configure(settings.FacesHolder, settings.Carried);

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

        // A model folder's optional {Name}.json: how the model rests in the world ("rest", degrees in Unity order: Z, then
        // X, then Y); the side of it that faces whoever holds it ("facesHolder", a direction in the model's own frame: a
        // bow's string, [-1, 0, 0]), none when any side may; where it is carried while put away ("carried": "hip", the
        // default, or "back": a bow) (<see cref="HeldLook"/>). "cutout": its base colour's alpha cuts it out, seen from
        // both sides (leaves, grass, a thatch's fringe).
        static (Vector3 Resting, Vector3? FacesHolder, CarriedAt Carried, bool Cutout) SettingsOf(string folder, string name)
        {
            var path = $"{folder}/{name}.json";
            if (!File.Exists(path))
                return (Vector3.zero, null, CarriedAt.Hip, false);

            var settings = JsonUtility.FromJson<ModelSettings>(File.ReadAllText(path));
            return (Triple(path, "rest", settings?.rest) ?? Vector3.zero, Triple(path, "facesHolder", settings?.facesHolder),
                Carried(path, settings?.carried), settings?.cutout ?? false);
        }

        static CarriedAt Carried(string path, string carried)
        {
            if (string.IsNullOrEmpty(carried))
                return CarriedAt.Hip;
            if (System.Enum.TryParse(carried, ignoreCase: true, out CarriedAt at))
                return at;
            throw new System.InvalidOperationException($"{path}: \"carried\" is \"hip\" or \"back\".");
        }

        static Vector3? Triple(string path, string key, float[] values)
        {
            if (values == null || values.Length == 0)
                return null;
            if (values.Length != 3)
                throw new System.InvalidOperationException($"{path}: \"{key}\" is three numbers, [x, y, z].");
            return new Vector3(values[0], values[1], values[2]);
        }

        [System.Serializable]
        sealed class ModelSettings
        {
            public float[] rest;
            public float[] facesHolder;
            public string carried;
            public bool cutout;
        }

        /// <summary>The alpha below which a cutout model's base colour is not drawn.</summary>
        public const float CutoutThreshold = 0.5f;

        static Material BuildMaterial(string folder, string name, bool cutout)
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
            // Cut out: the base colour's alpha clips it, and both faces draw (a leaf card is one quad).
            material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            material.SetFloat("_Cutoff", CutoutThreshold);
            material.SetFloat("_Cull", (float)(cutout ? CullMode.Off : CullMode.Back));
            material.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
            material.renderQueue = (int)(cutout ? RenderQueue.AlphaTest : RenderQueue.Geometry);
            MaterialBuilder.SetKeyword(material, "_ALPHATEST_ON", cutout);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
