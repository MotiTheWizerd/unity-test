using UnityEditor;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// How characters import, decided once for the folder rather than by hand per file: every model under
    /// Content/Characters is a humanoid (Unity maps a Mixamo skeleton to its human rig, so any humanoid animation plays
    /// on it), makes its own avatar, and brings no materials, cameras or lights (<see cref="CharacterBuilder"/> gives it
    /// its material). A replaceable body's heads (its folder's <see cref="HeadsFolder"/>) are pieces of it, not
    /// characters: each a skinned mesh on a copy of its skeleton, worn on the body's own bones
    /// (<see cref="Forge.Play.SkinnedPiece"/>), so it keeps its bones and makes no avatar; and its materials keep their
    /// names, which say which slot draws the body's skin and which the head's own maps (Unity doesn't keep the file's
    /// slot order: <see cref="CharacterBuilder"/> gives each by its name).
    /// </summary>
    sealed class CharacterImport : AssetPostprocessor
    {
        public const string CharactersFolder = "Assets/_Project/Content/Characters";

        /// <summary>A character's heads, in its folder (Content/Characters/{Name}/Heads).</summary>
        public const string HeadsFolder = "Heads";

        /// <summary>Asset key of the character in the folder <paramref name="folderName"/>: "VikingF1" → "characters/viking_f1".</summary>
        public static string KeyFor(string folderName) => "characters/" + ModelImport.SnakeCase(folderName);

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CharactersFolder + "/", System.StringComparison.Ordinal))
                return;

            var importer = (ModelImporter)assetImporter;
            if (assetPath.Contains($"/{HeadsFolder}/", System.StringComparison.Ordinal))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
            }

            importer.importCameras = false;
            importer.importLights = false;
            importer.useFileScale = true;
        }
    }
}
