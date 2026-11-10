using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// How sounds import, decided once for the folder: everything under Content/Sounds is short and played often (a
    /// step, a blow), so it is unpacked when loaded rather than as it plays, and readable, so Build Sounds can split a
    /// recording (<see cref="SoundBuilder"/>). Sounds heard from a place in the world are one channel, as a place is one
    /// point; music and ambience keep their stereo.
    /// </summary>
    sealed class SoundImport : AssetPostprocessor
    {
        public const string SoundsFolder = "Assets/_Project/Content/Sounds";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(SoundsFolder + "/", System.StringComparison.Ordinal))
                return;

            var importer = (AudioImporter)assetImporter;
            var group = assetPath.Substring(SoundsFolder.Length + 1).Split('/')[0];
            importer.forceToMono = group != nameof(Forge.Play.Sound.SoundBus.Music) && group != nameof(Forge.Play.Sound.SoundBus.Ambience);
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}
