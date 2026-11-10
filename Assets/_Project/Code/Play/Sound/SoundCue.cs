using System;
using UnityEngine;

namespace Forge.Play.Sound
{
    /// <summary>
    /// One sound the game plays by key ("footsteps/walk"), as Build Sounds makes it from a folder under Content/Sounds:
    /// its takes (one picked each time, never the same twice running: <see cref="TakePicker"/>), how loud, how much
    /// each play varies in loudness and pitch so a repeat never sounds stamped, the mix it plays through, and how far it
    /// carries in the world (none: heard alike everywhere, a menu's click).
    /// </summary>
    public sealed class SoundCue : ScriptableObject
    {
        [SerializeField, Tooltip("The recordings it plays, one picked each time.")]
        AudioClip[] _takes = Array.Empty<AudioClip>();

        [SerializeField, Range(0f, 1f), Tooltip("How loud it plays.")]
        float _volume = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("How much quieter a play may be, a share of the volume.")]
        float _volumeJitter = 0.1f;

        [SerializeField, Range(0f, 0.5f), Tooltip("How far a play's pitch may stray either way, a share of it.")]
        float _pitchJitter = 0.05f;

        [SerializeField, Tooltip("The mix it plays through.")]
        SoundBus _bus;

        [SerializeField, Min(0f), Tooltip("Meters it carries in the world; 0: heard alike everywhere (not in the world).")]
        float _range = 25f;

        public AudioClip[] Takes => _takes;

        public float Volume => _volume;

        public float VolumeJitter => _volumeJitter;

        public float PitchJitter => _pitchJitter;

        public SoundBus Bus => _bus;

        public float Range => _range;

        /// <summary>Whether it is heard from where it plays in the world, rather than alike everywhere.</summary>
        public bool InWorld => _range > 0f;

        /// <summary>Sets it all, as Build Sounds makes it.</summary>
        public void Configure(AudioClip[] takes, float volume, float volumeJitter, float pitchJitter, SoundBus bus, float range)
        {
            _takes = takes ?? throw new ArgumentNullException(nameof(takes));
            _volume = Mathf.Clamp01(volume);
            _volumeJitter = Mathf.Clamp01(volumeJitter);
            _pitchJitter = Mathf.Clamp(pitchJitter, 0f, 0.5f);
            _bus = bus;
            _range = Mathf.Max(0f, range);
        }
    }
}
