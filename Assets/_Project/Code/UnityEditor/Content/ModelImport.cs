using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// How static models import, decided once for the folder rather than by hand per file: every model under
    /// Content/Models (a folder each, see <see cref="ModelBuilder"/>) brings no materials, cameras, lights or animation
    /// (the builder gives it its material). Its parts follow the art repo's naming, as Unity's and Unreal's importers do:
    /// meshes named <c>…_LOD0</c>, <c>…_LOD1</c>, … are its levels of detail (Unity groups them itself; they get Forge's
    /// distances here, <see cref="LodHeight"/>), and a mesh named <c>…_COL</c> is a convex collider: it shapes the
    /// physics and is never drawn.
    /// </summary>
    sealed class ModelImport : AssetPostprocessor
    {
        public const string ModelsFolder = "Assets/_Project/Content/Models";

        /// <summary>The face editor's heads, one folder each, imported as the models are but placed nowhere in the world.</summary>
        public const string HeadsFolder = "Assets/_Project/Content/Heads";
        const string ColliderSuffix = "_COL";

        // A model shows its full detail while it stands at least this share of the screen's height, and is culled below
        // the last; the levels between step down evenly (geometrically). A 1 m sword: full detail within ~6 m, gone past ~85 m.
        const float FullDetailHeight = 0.15f;
        const float CullHeight = 0.01f;

        /// <summary>Asset key of the model in the folder <paramref name="folderName"/>: "VikingSword" → "models/viking_sword".</summary>
        public static string KeyFor(string folderName) => "models/" + SnakeCase(folderName);

        void OnPreprocessModel()
        {
            if (!IsModel(assetPath))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.useFileScale = true;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!IsModel(assetPath))
                return;

            foreach (var part in root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.EndsWith(ColliderSuffix, StringComparison.Ordinal)).ToArray())
                MakeCollider(part);
            // A model imported without animation draws its meshes plainly, which can't drive blend shapes: a head's face
            // sliders are its shapes, so a mesh that has them draws skinned.
            foreach (var part in root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && f.sharedMesh.blendShapeCount > 0).ToArray())
                DrawSkinned(part);
            if (root.TryGetComponent<LODGroup>(out var group))
                SetDistances(group);
        }

        /// <summary>
        /// The screen height share below which level <paramref name="level"/> of <paramref name="levels"/> gives way to the
        /// next (the last: to nothing).
        /// </summary>
        public static float LodHeight(int level, int levels) =>
            levels <= 1 ? CullHeight : FullDetailHeight * Mathf.Pow(CullHeight / FullDetailHeight, level / (levels - 1f));

        static bool IsModel(string path) =>
            path.StartsWith(ModelsFolder + "/", StringComparison.Ordinal) || path.StartsWith(HeadsFolder + "/", StringComparison.Ordinal);

        static void DrawSkinned(MeshFilter part)
        {
            var mesh = part.sharedMesh;
            var plain = part.GetComponent<MeshRenderer>();
            var materials = plain != null ? plain.sharedMaterials : Array.Empty<Material>();
            var holder = part.gameObject;
            if (plain != null)
                UnityEngine.Object.DestroyImmediate(plain);
            UnityEngine.Object.DestroyImmediate(part);
            var skinned = holder.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            skinned.sharedMaterials = materials;
        }

        static void MakeCollider(MeshFilter part)
        {
            var collider = part.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = part.sharedMesh;
            // Convex, as the art repo builds them: a moving body (an item dropped) can only carry convex mesh colliders.
            collider.convex = true;
            if (part.TryGetComponent<MeshRenderer>(out var renderer))
                UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(part);
        }

        static void SetDistances(LODGroup group)
        {
            var lods = group.GetLODs();
            for (var i = 0; i < lods.Length; i++)
                lods[i].screenRelativeTransitionHeight = LodHeight(i, lods.Length);
            group.SetLODs(lods);
            group.RecalculateBounds();
        }

        // "VikingSword" → "viking_sword"; a run of capitals stays one word ("LODTable" → "lod_table").
        internal static string SnakeCase(string name)
        {
            var snake = new StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                var wordStart = char.IsUpper(c) && i > 0
                    && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1])));
                if (wordStart && snake.Length > 0 && snake[snake.Length - 1] != '_')
                    snake.Append('_');
                snake.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');
            }

            return snake.ToString();
        }
    }
}
