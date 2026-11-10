using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Forge.Play.Sound;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Dev tool: turns each sound folder under Content/Sounds into a cue the game plays by key, as Build Models does
    /// for models. A folder holding recordings (.wav, .mp3, .ogg) is a cue, keyed by its path in lowercase
    /// (Footsteps/Walk: "footsteps/walk"), its takes its recordings, or, when its optional {Name}.json says
    /// <c>"split": true</c>, the separate sounds found in them (<see cref="RecordingSplitter"/>: a run of steps becomes
    /// a step a take, each at least "gap" seconds after the last began: 0.15 unless the json says), baked to
    /// _Built/{Path}/{Name}_NN.wav, each evened out to the same loudness and faded at both ends so none clicks; a scuff
    /// shorter than a step is left out. The json may also set "volume", "volumeJitter", "pitchJitter" and "range"
    /// (meters it carries; 0 is heard alike everywhere). Its bus is its top folder when that names one (Footsteps),
    /// else Effects. The cues
    /// go in the sound catalog (Content/SoundCatalog), and the settings (Settings/Sound) send each bus through the
    /// mixer's group of its name. Existing assets are updated in place, so their GUIDs survive a rebuild.
    /// </summary>
    static class SoundBuilder
    {
        public const string CatalogPath = "Assets/_Project/Content/SoundCatalog.asset";
        public const string SettingsPath = "Assets/_Project/Settings/Sound/SoundSettings.asset";
        public const string MixerPath = "Assets/_Project/Settings/Sound/Forge.mixer";
        const string BuiltFolder = "_Built";

        // Frames the split listens in, and the shortest and longest a sound split out may be, seconds.
        const float FrameSeconds = 0.01f;
        const float ShortestSeconds = 0.08f;
        const float LongestSeconds = 0.8f;

        // How loud each baked take's loudest point is, and how long it fades in and out, seconds.
        const float TakePeak = 0.9f;
        const float FadeIn = 0.003f;
        const float FadeOut = 0.03f;

        static readonly string[] Recordings = { ".wav", ".mp3", ".ogg" };

        [MenuItem("Forge/Sounds/Build Sounds")]
        static void Build()
        {
            var catalog = LoadOrCreate<SoundCatalog>(CatalogPath);
            var built = 0;
            foreach (var folder in CueFolders(SoundImport.SoundsFolder))
            {
                var cue = BuildCue(folder);
                if (cue == null)
                    continue;
                catalog.Set(KeyOf(folder), cue);
                built++;
            }

            EditorUtility.SetDirty(catalog);
            Wire(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Forge] Built {built} sound(s) under {SoundImport.SoundsFolder}; {CatalogPath} has {catalog.Count} entries.");
        }

        /// <summary>The key a cue folder plays by: its path under Content/Sounds, lowercase ("footsteps/walk").</summary>
        public static string KeyOf(string folder) =>
            folder.Substring(SoundImport.SoundsFolder.Length + 1).Replace('\\', '/').ToLowerInvariant();

        // Every folder under `root` (but what is built) holding recordings of its own.
        static IEnumerable<string> CueFolders(string root)
        {
            foreach (var folder in AssetDatabase.GetSubFolders(root).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (Path.GetFileName(folder) == BuiltFolder)
                    continue;
                if (RecordingsIn(folder).Any())
                    yield return folder;
                foreach (var deeper in CueFolders(folder))
                    yield return deeper;
            }
        }

        static IEnumerable<string> RecordingsIn(string folder) =>
            Directory.GetFiles(folder)
                .Where(file => Recordings.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .Select(file => file.Replace('\\', '/'))
                .OrderBy(file => file, StringComparer.Ordinal);

        static SoundCue BuildCue(string folder)
        {
            var name = Path.GetFileName(folder);
            var settings = SettingsOf(folder, name);
            var sources = RecordingsIn(folder).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(clip => clip != null).ToArray();
            var takes = settings.split ? Split(folder, name, sources, settings) : sources;
            if (takes.Length == 0)
            {
                Debug.LogWarning($"[Forge] {folder} has no sound to play; skipped.");
                return null;
            }

            var path = $"{BuiltPath(folder)}.asset";
            var cue = LoadOrCreate<SoundCue>(path);
            cue.Configure(takes, settings.volume, settings.volumeJitter, settings.pitchJitter, BusOf(folder), settings.range);
            EditorUtility.SetDirty(cue);
            return cue;
        }

        // The separate sounds of each recording, baked to their own files beside the cue (the old ones cleared first).
        static AudioClip[] Split(string folder, string name, AudioClip[] sources, CueSettings settings)
        {
            var into = BuiltPath(folder);
            EnsureFolder(into);
            foreach (var old in Directory.GetFiles(into, "*.wav"))
                AssetDatabase.DeleteAsset(old.Replace('\\', '/'));

            var written = new List<string>();
            foreach (var source in sources)
            {
                var samples = new float[source.samples * source.channels];
                if (!source.GetData(samples, 0))
                {
                    Debug.LogWarning($"[Forge] {AssetDatabase.GetAssetPath(source)} can't be read; left out of {folder}.");
                    continue;
                }

                var mono = Mono(samples, source.channels);
                var frame = Mathf.Max(1, Mathf.RoundToInt(source.frequency * FrameSeconds));
                var envelope = RecordingSplitter.Envelope(mono, 1, frame);
                var sounds = RecordingSplitter.Split(envelope, Mathf.RoundToInt(settings.gap / FrameSeconds),
                    Mathf.RoundToInt(LongestSeconds / FrameSeconds), Mathf.RoundToInt(ShortestSeconds / FrameSeconds));
                foreach (var (start, end) in sounds)
                {
                    var take = Take(mono, start * frame, Mathf.Min(mono.Length, end * frame), source.frequency);
                    var file = $"{into}/{name}_{written.Count + 1:00}.wav";
                    WavFile.WriteMono(file, take, source.frequency);
                    written.Add(file);
                }
            }

            foreach (var file in written)
                AssetDatabase.ImportAsset(file);
            return written.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(clip => clip != null).ToArray();
        }

        // Samples [from, to) of a recording, its loudest point brought to TakePeak, faded in and out.
        static float[] Take(float[] mono, int from, int to, int frequency)
        {
            var take = new float[Math.Max(0, to - from)];
            Array.Copy(mono, from, take, 0, take.Length);
            var peak = take.Length > 0 ? take.Max(x => Math.Abs(x)) : 0f;
            var gain = peak > 1e-5f ? TakePeak / peak : 1f;
            var fadeIn = Mathf.Max(1, Mathf.RoundToInt(frequency * FadeIn));
            var fadeOut = Mathf.Max(1, Mathf.RoundToInt(frequency * FadeOut));
            for (var i = 0; i < take.Length; i++)
            {
                var envelope = Mathf.Min(1f, Mathf.Min((float)i / fadeIn, (float)(take.Length - 1 - i) / fadeOut));
                take[i] *= gain * envelope;
            }

            return take;
        }

        static float[] Mono(float[] samples, int channels)
        {
            if (channels == 1)
                return samples;
            var mono = new float[samples.Length / channels];
            for (var i = 0; i < mono.Length; i++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++)
                    sum += samples[i * channels + c];
                mono[i] = sum / channels;
            }

            return mono;
        }

        // A cue's bus: its top folder, when that names one.
        static SoundBus BusOf(string folder)
        {
            var top = KeyOf(folder).Split('/')[0];
            return Enum.TryParse(top, ignoreCase: true, out SoundBus bus) ? bus : SoundBus.Effects;
        }

        // Where a cue's built assets go: under _Built, by its path ("…/Sounds/_Built/Footsteps/Walk").
        static string BuiltPath(string folder) =>
            $"{SoundImport.SoundsFolder}/{BuiltFolder}/{folder.Substring(SoundImport.SoundsFolder.Length + 1)}";

        // The settings point at the catalog, and send each bus through the mixer's group of its name.
        static void Wire(SoundCatalog catalog)
        {
            var settings = LoadOrCreate<SoundSettings>(SettingsPath);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            var routes = new List<(SoundBus, AudioMixerGroup)>();
            if (mixer != null)
            {
                foreach (SoundBus bus in Enum.GetValues(typeof(SoundBus)))
                {
                    var group = mixer.FindMatchingGroups(bus.ToString()).FirstOrDefault(g => g.name == bus.ToString());
                    if (group != null)
                        routes.Add((bus, group));
                }
            }
            else
                Debug.LogWarning($"[Forge] No mixer at {MixerPath}; every sound plays straight out.");

            settings.Configure(catalog, routes.ToArray());
            EditorUtility.SetDirty(settings);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // A cue folder's optional {Name}.json.
        static CueSettings SettingsOf(string folder, string name)
        {
            var path = $"{folder}/{name}.json";
            return File.Exists(path) ? JsonUtility.FromJson<CueSettings>(File.ReadAllText(path)) ?? new CueSettings() : new CueSettings();
        }

        [Serializable]
        sealed class CueSettings
        {
            public bool split;
            public float gap = 0.15f;
            public float volume = 1f;
            public float volumeJitter = 0.1f;
            public float pitchJitter = 0.05f;
            public float range = 25f;
        }
    }
}
