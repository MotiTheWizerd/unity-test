using System.Collections.Generic;
using System.IO;
using System.Linq;
using Forge.Play;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Dev tool: turns each character folder under Content/Characters into a ready character, as the material builder does
    /// for material folders. A folder {Name} holds the rigged model {Name}.fbx (imported as a humanoid by
    /// <see cref="CharacterImport"/>; feet at the origin, facing +Z, in meters), its colour {Name}_Color.png and, if it has
    /// one, its normal map {Name}_Normal.png. The tool makes a URP Lit material {Name}.mat from them and a prefab
    /// {Name}.prefab, a variant of the model (so a re-exported model updates it), wearing the material, with an Animator on
    /// the model's humanoid avatar playing its move set (<see cref="HumanoidAnimatorBuilder"/>, built first: the one an
    /// optional {Name}.json names, else the base Humanoid set) and no root motion (the body moves it). Existing assets are
    /// updated in place,
    /// so their GUIDs, and everything referencing them (the player profile), survive a rebuild.
    /// </summary>
    static class CharacterBuilder
    {
        const int TextureSize = 2048;

        [MenuItem("Forge/Characters/Build Characters")]
        static void Build()
        {
            // The clips' reference rig first: truing it reimports every clip, which the controllers are built from.
            if (HumanoidTPose.Straighten(AnimationImport.ReferenceRig, out var trued))
            {
                Debug.Log($"[Forge] {AnimationImport.ReferenceRig}: T-pose trued ({trued}); the clips reimport against it.");
                AssetDatabase.Refresh();
            }

            var moveSets = HumanoidAnimatorBuilder.BuildAll();
            var catalog = PlaceholderContentGenerator.LoadOrCreateCatalog();
            var npcs = CoreModels.Npcs.Select(npc => npc.Folder).ToHashSet();
            var built = new List<GameObject>();
            foreach (var folder in AssetDatabase.GetSubFolders(CharacterImport.CharactersFolder).OrderBy(f => f, System.StringComparer.Ordinal))
            {
                var name = Path.GetFileName(folder);
                if (HumanoidTPose.Straighten($"{folder}/{name}.fbx", out trued))
                    Debug.Log($"[Forge] {name}: T-pose trued ({trued}).");
                var settings = SettingsOf(folder, name);
                var prefab = BuildCharacter(folder, name, MoveSetOf(name, settings, moveSets), settings.textureSize > 0 ? settings.textureSize : TextureSize);
                if (prefab == null)
                    continue;
                // An NPC is placed in the world from the catalog, as a model is.
                if (npcs.Contains(name))
                    catalog.Set(CharacterImport.KeyFor(name), prefab);
                built.Add(prefab);
            }

            EditorUtility.SetDirty(catalog);
            ListPlayerLooks(built);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Forge] Built {built.Count} character(s) under {CharacterImport.CharactersFolder}.");
        }

        // Dev, while animation and combat are tuned: the player can be any character (the next-look key, F8).
        static void ListPlayerLooks(IReadOnlyCollection<GameObject> characters)
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(PlayerProfile)}"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<PlayerProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null)
                    continue;
                profile.SetLooks(characters);
                EditorUtility.SetDirty(profile);
            }
        }

        // A character folder's optional {Name}.json: { "moves": "Guard", "textureSize": 4096 }.
        static CharacterSettings SettingsOf(string folder, string name)
        {
            var settingsPath = $"{folder}/{name}.json";
            return (File.Exists(settingsPath) ? JsonUtility.FromJson<CharacterSettings>(File.ReadAllText(settingsPath)) : null)
                   ?? new CharacterSettings();
        }

        /// <summary>
        /// The controller of the move set the character plays: the one its settings name (<c>"moves": "Guard"</c>), else
        /// the base set's.
        /// </summary>
        static RuntimeAnimatorController MoveSetOf(string name, CharacterSettings settings, IReadOnlyDictionary<string, AnimatorController> moveSets)
        {
            var set = string.IsNullOrWhiteSpace(settings.moves) ? HumanoidAnimatorBuilder.BaseSet : settings.moves.Trim();

            if (moveSets.TryGetValue(set, out var controller))
                return controller;
            Debug.LogWarning($"[Forge] {name} plays move set '{set}', which has no animator under {AnimationImport.AnimationsFolder}; it plays {HumanoidAnimatorBuilder.BaseSet}.");
            return moveSets.TryGetValue(HumanoidAnimatorBuilder.BaseSet, out var fallback) ? fallback : null;
        }

        [System.Serializable]
        sealed class CharacterSettings
        {
            public string moves;
            // Its textures' largest size; 0: TextureSize. The player is seen closest, so it may wear more.
            public int textureSize;
        }

        /// <summary>
        /// The prefab built for the character folder <paramref name="folder"/>, playing <paramref name="animations"/>
        /// (none: it stands in its rest pose), its textures at most <paramref name="textureSize"/>, or null when it isn't
        /// a character.
        /// </summary>
        public static GameObject BuildCharacter(string folder, string name, RuntimeAnimatorController animations, int textureSize = TextureSize)
        {
            var modelPath = $"{folder}/{name}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning($"[Forge] {folder} has no {name}.fbx; skipped.");
                return null;
            }

            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman || !avatar.isValid)
            {
                Debug.LogError($"[Forge] {modelPath} did not import as a humanoid (is it rigged?); skipped.");
                return null;
            }

            var material = BuildMaterial(folder, name, textureSize);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }

                if (!instance.TryGetComponent<Animator>(out var animator))
                    animator = instance.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = animations;
                animator.applyRootMotion = false;
                // Off-screen characters don't pose their bones; the player is always on screen when it shows.
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // After the animator: binding it puts the bones back as the file has them.
                foreach (var skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    RestInBindPose(skinned);
                return PrefabUtility.SaveAsPrefabAsset(instance, $"{folder}/{name}.prefab");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // A model downloaded with an animation (Mixamo's "with skin") stands in that animation's first frame; the
        // character's rest pose is the one it was rigged in, which the mesh's bind poses record. Parents go before their
        // children, so each bone is placed after the one it hangs from.
        static void RestInBindPose(SkinnedMeshRenderer skinned)
        {
            var bones = skinned.bones;
            var bindPoses = skinned.sharedMesh.bindposes;
            var meshToWorld = skinned.transform.localToWorldMatrix;
            foreach (var i in Enumerable.Range(0, bones.Length).Where(i => bones[i] != null).OrderBy(i => Depth(bones[i])))
            {
                var pose = meshToWorld * bindPoses[i].inverse;
                bones[i].SetPositionAndRotation(pose.GetColumn(3), pose.rotation);
            }
        }

        static int Depth(Transform transform)
        {
            var depth = 0;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                depth++;
            return depth;
        }

        static Material BuildMaterial(string folder, string name, int textureSize)
        {
            var colorPath = $"{folder}/{name}_Color.png";
            var normalPath = $"{folder}/{name}_Normal.png";
            var hasColor = File.Exists(colorPath);
            var hasNormal = File.Exists(normalPath);
            if (hasColor)
                TextureMaps.Configure(colorPath, TextureImporterType.Default, sRGB: true, textureSize);
            if (hasNormal)
                TextureMaps.Configure(normalPath, TextureImporterType.NormalMap, sRGB: false, textureSize);

            var path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", hasColor ? AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath) : null);
            material.SetTexture("_BumpMap", hasNormal ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null);
            if (hasNormal)
                material.EnableKeyword("_NORMALMAP");
            else
                material.DisableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", 0f);
            // Cloth, leather and skin: matte, no wet sheen.
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
