using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Forge.Play.Sound
{
    /// <summary>
    /// How the game sounds (Settings/Sound): the sounds by key, the mix each kind plays through (Forge.mixer's groups),
    /// and how many may play at once. Tuned in the Inspector, never in code.
    /// </summary>
    [CreateAssetMenu(menuName = "Forge/Sound Settings", fileName = "SoundSettings")]
    public sealed class SoundSettings : ScriptableObject
    {
        [SerializeField, Tooltip("The sounds, by key (Content/SoundCatalog, made by Forge → Sounds → Build Sounds).")]
        SoundCatalog _catalog;

        [SerializeField, Tooltip("The mixer group each bus plays through; a bus with none plays through Effects', else straight out.")]
        Route[] _routes = Array.Empty<Route>();

        [SerializeField, Range(4, 64), Tooltip("How many sounds may play at once; past it, the oldest gives way.")]
        int _voices = 24;

        public SoundCatalog Catalog => _catalog;

        public int Voices => _voices;

        /// <summary>The mixer group <paramref name="bus"/> plays through: its own, else Effects', else none.</summary>
        public AudioMixerGroup GroupOf(SoundBus bus) => Find(bus) ?? Find(SoundBus.Effects);

        AudioMixerGroup Find(SoundBus bus)
        {
            foreach (var route in _routes)
            {
                if (route.Bus == bus && route.Group != null)
                    return route.Group;
            }

            return null;
        }

        /// <summary>Sets the catalog and the routes, as the sound tools wire them.</summary>
        public void Configure(SoundCatalog catalog, (SoundBus Bus, AudioMixerGroup Group)[] routes)
        {
            _catalog = catalog;
            _routes = Array.ConvertAll(routes ?? Array.Empty<(SoundBus, AudioMixerGroup)>(), r => new Route { Bus = r.Bus, Group = r.Group });
        }

        [Serializable]
        struct Route
        {
            public SoundBus Bus;
            public AudioMixerGroup Group;
        }
    }
}
