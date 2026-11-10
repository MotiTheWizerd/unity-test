using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Forge.Play.Sound
{
    /// <summary>
    /// Plays the game's sounds: a fixed set of voices (one AudioSource each, <see cref="SoundSettings.Voices"/>) made
    /// once, so playing allocates nothing; a sound takes a free voice, else the one playing longest gives way. Each
    /// play picks a take (<see cref="TakePicker"/>) and strays a little in loudness and pitch, through its bus's mix,
    /// from where it happens in the world or alike everywhere (<see cref="SoundCue.InWorld"/>). What plays when is
    /// its listeners' business (<see cref="FootstepView"/>, the game's happenings through <see cref="SoundDirector"/>);
    /// this only plays.
    /// </summary>
    public sealed class SoundPlayer : IDisposable
    {
        readonly SoundSettings _settings;
        readonly GameObject _root;
        readonly AudioSource[] _voices;
        readonly float[] _startedAt;
        readonly int[] _plays;
        readonly Dictionary<SoundCue, TakePicker> _pickers = new Dictionary<SoundCue, TakePicker>();
        readonly Random _random = new Random();

        public SoundPlayer(SoundSettings settings)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _root = new GameObject("Sounds");
            _voices = new AudioSource[settings.Voices];
            _startedAt = new float[settings.Voices];
            _plays = new int[settings.Voices];
            for (var i = 0; i < _voices.Length; i++)
            {
                var voice = new GameObject("Voice " + i).AddComponent<AudioSource>();
                voice.transform.SetParent(_root.transform, false);
                voice.playOnAwake = false;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voice.dopplerLevel = 0f;
                _voices[i] = voice;
            }
        }

        /// <summary>The sounds it plays, by key.</summary>
        public SoundCatalog Catalog => _settings.Catalog;

        /// <summary>
        /// Plays the first of <paramref name="options"/> it has a sound for (most particular first), at its gain, at
        /// <paramref name="where"/> (scene space); false when it has none of them.
        /// </summary>
        public bool PlayFirst(IEnumerable<(string Key, float Gain)> options, Vector3 where)
        {
            foreach (var (key, gain) in options)
            {
                if (Play(key, where, gain).Started)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Plays the sound of <paramref name="key"/> at <paramref name="where"/> (scene space), <paramref name="gain"/>
        /// times as loud: what <see cref="Stop"/> cuts it short by; <see cref="SoundHandle.None"/> when it has none.
        /// </summary>
        public SoundHandle Play(string key, Vector3 where, float gain = 1f) =>
            Catalog != null && Catalog.TryGet(key, out var cue) ? Play(cue, where, gain) : SoundHandle.None;

        /// <summary>Plays <paramref name="cue"/> at <paramref name="where"/> (scene space), <paramref name="gain"/> times as loud: what <see cref="Stop"/> cuts it short by.</summary>
        public SoundHandle Play(SoundCue cue, Vector3 where, float gain = 1f)
        {
            if (cue == null || cue.Takes.Length == 0)
                return SoundHandle.None;
            if (!_pickers.TryGetValue(cue, out var picker))
                _pickers.Add(cue, picker = new TakePicker(_random));
            var take = cue.Takes[picker.Next(cue.Takes.Length)];
            if (take == null)
                return SoundHandle.None;

            var index = FreeVoice();
            var voice = _voices[index];
            voice.Stop();
            voice.clip = take;
            voice.outputAudioMixerGroup = _settings.GroupOf(cue.Bus);
            voice.volume = Mathf.Clamp01(cue.Volume * gain * (1f - cue.VolumeJitter * (float)_random.NextDouble()));
            voice.pitch = 1f + cue.PitchJitter * (2f * (float)_random.NextDouble() - 1f);
            voice.spatialBlend = cue.InWorld ? 1f : 0f;
            voice.minDistance = 1f;
            voice.maxDistance = cue.InWorld ? Mathf.Max(1f, cue.Range) : 500f;
            voice.transform.position = where;
            voice.Play();
            _startedAt[index] = Time.unscaledTime;
            return new SoundHandle(index, ++_plays[index]);
        }

        /// <summary>Cuts <paramref name="sound"/> short (a bow's creak as it is loosed); nothing once its voice plays another.</summary>
        public void Stop(SoundHandle sound)
        {
            if (sound.Voice >= 0 && sound.Voice < _voices.Length && _plays[sound.Voice] == sound.Play)
                _voices[sound.Voice].Stop();
        }

        // A voice that isn't playing, else the one that has played longest.
        int FreeVoice()
        {
            var oldest = 0;
            for (var i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].isPlaying)
                    return i;
                if (_startedAt[i] < _startedAt[oldest])
                    oldest = i;
            }

            return oldest;
        }

        public void Dispose()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }
    }
}
