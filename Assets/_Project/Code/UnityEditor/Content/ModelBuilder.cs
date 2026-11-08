using System.Collections.Generic;
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
    /// {Name}_BaseColor.png carries its alpha; a map without alpha may be a .jpg instead. A model of several parts (a ship's
    /// hull, rigging and sails) names its meshes {Name}_{Part}_LOD0, … and each part's maps {Name}_{Part}_BaseColor, …: each
    /// part draws with its own material {Name}_{Part}.mat. The tool makes a URP Lit material {Name}.mat from them and a
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
            var materials = new Dictionary<string, Material>();
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
                    var part = PartOf(renderer.name, name);
                    var key = MaterialName(name, part);
                    if (!materials.TryGetValue(key, out var material))
                        materials[key] = material = BuildMaterial(folder, key, settings.SurfaceOf(part));
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
        // both sides (leaves, grass, a thatch's fringe); "cutoutParts" names the parts that are ([ "Sails" ]), and
        // "transparentParts" the parts seen through by their base colour's alpha (a lantern's glass).
        static Settings SettingsOf(string folder, string name)
        {
            var path = $"{folder}/{name}.json";
            if (!File.Exists(path))
                return new Settings(Vector3.zero, null, CarriedAt.Hip, false, new string[0], new string[0]);

            var settings = JsonUtility.FromJson<ModelSettings>(File.ReadAllText(path));
            return new Settings(Triple(path, "rest", settings?.rest) ?? Vector3.zero, Triple(path, "facesHolder", settings?.facesHolder),
                Carried(path, settings?.carried), settings?.cutout ?? false, settings?.cutoutParts ?? new string[0],
                settings?.transparentParts ?? new string[0]);
        }

        sealed class Settings
        {
            public readonly Vector3 Resting;
            public readonly Vector3? FacesHolder;
            public readonly CarriedAt Carried;
            readonly bool _cutout;
            readonly string[] _cutoutParts;
            readonly string[] _transparentParts;

            public Settings(Vector3 resting, Vector3? facesHolder, CarriedAt carried, bool cutout, string[] cutoutParts, string[] transparentParts)
            {
                Resting = resting;
                FacesHolder = facesHolder;
                Carried = carried;
                _cutout = cutout;
                _cutoutParts = cutoutParts;
                _transparentParts = transparentParts;
            }

            /// <summary>How the part <paramref name="part"/> (null: the whole model) draws.</summary>
            public Surface SurfaceOf(string part) =>
                part != null && _transparentParts.Contains(part) ? Surface.Transparent
                : _cutout || (part != null && _cutoutParts.Contains(part)) ? Surface.Cutout
                : Surface.Opaque;
        }

        enum Surface { Opaque, Cutout, Transparent }

        const string LodMarker = "_LOD";

        /// <summary>
        /// The part a renderer of the model <paramref name="model"/> draws: "DutchShip_Hull_LOD1" → "Hull"; null for the
        /// model's own mesh ("DutchShip", "DutchShip_LOD0"), which draws with the model's one material.
        /// </summary>
        public static string PartOf(string rendererName, string model)
        {
            var lod = rendererName.LastIndexOf(LodMarker, System.StringComparison.Ordinal);
            var stem = lod >= 0 && rendererName.Substring(lod + LodMarker.Length).All(char.IsDigit) && lod + LodMarker.Length < rendererName.Length
                ? rendererName.Substring(0, lod)
                : rendererName;
            return stem.StartsWith(model + "_", System.StringComparison.Ordinal) && stem.Length > model.Length + 1
                ? stem.Substring(model.Length + 1)
                : null;
        }

        /// <summary>The material (and texture name stem) of a model's part: "DutchShip_Hull", or the model's own name.</summary>
        public static string MaterialName(string model, string part) => part == null ? model : $"{model}_{part}";

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
            public string[] cutoutParts;
            public string[] transparentParts;
        }

        /// <summary>The alpha below which a cutout model's base colour is not drawn.</summary>
        public const float CutoutThreshold = 0.5f;

        // A map {name}_{map}, a .png or (without alpha, for size) a .jpg; null when there is neither.
        static string MapPath(string folder, string name, string map) =>
            new[] { ".png", ".jpg" }.Select(ext => $"{folder}/{name}_{map}{ext}").FirstOrDefault(File.Exists);

        static Material BuildMaterial(string folder, string name, Surface surface)
        {
            var colorPath = MapPath(folder, name, "BaseColor");
            var metalPath = MapPath(folder, name, "MetallicSmoothness");
            var normalPath = MapPath(folder, name, "Normal");
            var occlusionPath = MapPath(folder, name, "Occlusion");
            var hasColor = colorPath != null;
            var hasMetal = metalPath != null;
            var hasNormal = normalPath != null;
            var hasOcclusion = occlusionPath != null;
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
            var cutout = surface == Surface.Cutout;
            material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            material.SetFloat("_Cutoff", CutoutThreshold);
            material.SetFloat("_Cull", (float)(cutout ? CullMode.Off : CullMode.Back));
            MaterialBuilder.SetKeyword(material, "_ALPHATEST_ON", cutout);
            // Transparent: the base colour's alpha blends it over what is behind (glass), drawn after the opaque world
            // and writing no depth.
            var transparent = surface == Surface.Transparent;
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);
            // URP Lit preserves specular on glass: it premultiplies by alpha itself, so its source factor is One (as URP
            // sets it on validating the material; another value is rewritten).
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            MaterialBuilder.SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
            material.renderQueue = (int)(transparent ? RenderQueue.Transparent : cutout ? RenderQueue.AlphaTest : RenderQueue.Geometry);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
