using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Whose voice a character's sounds are made in, as its {Name}.json says (<c>"voice": "female"</c>); the character build
    /// puts it on the prefab. A sound with a version in that voice plays it (<see cref="Sound.SoundDirector"/>: a woman's
    /// jump is her breath); one without plays as it is. A character without one has no voice of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterVoice : MonoBehaviour
    {
        [SerializeField, Tooltip("The voice its sounds are made in (\"female\"); empty: none of its own.")]
        string _voice;

        /// <summary>The voice, or null when it has none.</summary>
        public string Voice => string.IsNullOrWhiteSpace(_voice) ? null : _voice.Trim();

        /// <summary>Sets it, as the character build reads it from the json.</summary>
        public void Configure(string voice) => _voice = voice;

        /// <summary>The voice of <paramref name="character"/>, or null when it has none.</summary>
        public static string Of(GameObject character)
        {
            var voice = character != null ? character.GetComponentInChildren<CharacterVoice>(true) : null;
            return voice != null ? voice.Voice : null;
        }
    }
}
