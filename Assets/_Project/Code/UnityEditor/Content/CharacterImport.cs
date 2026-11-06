using UnityEditor;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// How characters import, decided once for the folder rather than by hand per file: every model under
    /// Content/Characters is a humanoid (Unity maps a Mixamo skeleton to its human rig, so any humanoid animation plays
    /// on it), makes its own avatar, and brings no materials, cameras or lights (<see cref="CharacterBuilder"/> gives it
    /// its material).
    /// </summary>
    sealed class CharacterImport : AssetPostprocessor
    {
        public const string CharactersFolder = "Assets/_Project/Content/Characters";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CharactersFolder + "/", System.StringComparison.Ordinal))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.useFileScale = true;
        }
    }
}
