using UnityEngine;

namespace Forge.World.Sky
{
    /// <summary>
    /// How a sky looks: its colors and the sun's light as gradients over the sun's height (from 10° below the
    /// horizon to straight overhead, so dawn, noon and dusk come from one table) and the material that draws it
    /// (Forge/Sky). Where the sun stands comes from the world clock. Config, not world data: swapping the profile
    /// restyles the sky without touching a plugin.
    /// </summary>
    [CreateAssetMenu(fileName = "Sky", menuName = "Forge/Sky Profile")]
    public sealed class SkyProfile : ScriptableObject
    {
        /// <summary>The sun's height at the left end of every gradient, in degrees.</summary>
        public const float LowestSun = -10f;

        /// <summary>The sun's height at the right end of every gradient: straight overhead.</summary>
        public const float HighestSun = 90f;

        [SerializeField, Tooltip("A material with the Forge/Sky shader; a copy is drawn, so this asset never changes at runtime.")]
        Material _material;

        [Header("By the sun's height: left = 10° below the horizon, right = overhead")]
        [SerializeField] Gradient _zenith = Over(
            (-10f, new Color(0.03f, 0.05f, 0.10f)), (0f, new Color(0.22f, 0.30f, 0.48f)), (12f, new Color(0.24f, 0.45f, 0.78f)), (90f, new Color(0.16f, 0.40f, 0.84f)));

        [SerializeField] Gradient _horizon = Over(
            (-10f, new Color(0.06f, 0.07f, 0.12f)), (0f, new Color(0.95f, 0.56f, 0.34f)), (10f, new Color(0.86f, 0.80f, 0.74f)), (25f, new Color(0.72f, 0.82f, 0.93f)), (90f, new Color(0.68f, 0.81f, 0.95f)));

        [SerializeField] Gradient _ground = Over(
            (-10f, new Color(0.03f, 0.03f, 0.04f)), (0f, new Color(0.30f, 0.24f, 0.22f)), (20f, new Color(0.38f, 0.39f, 0.40f)), (90f, new Color(0.40f, 0.41f, 0.42f)));

        [SerializeField] Gradient _sunLight = Over(
            (-10f, new Color(1f, 0.45f, 0.25f)), (0f, new Color(1f, 0.55f, 0.30f)), (12f, new Color(1f, 0.85f, 0.68f)), (35f, new Color(1f, 0.96f, 0.90f)), (90f, new Color(1f, 0.98f, 0.95f)));

        [SerializeField, Tooltip("The sun light's intensity by its height (in degrees), from below the horizon to overhead.")]
        AnimationCurve _sunIntensity = new AnimationCurve(new Keyframe(-4f, 0f), new Keyframe(2f, 0.5f), new Keyframe(15f, 1.6f), new Keyframe(45f, 2f), new Keyframe(90f, 2.2f));

        [SerializeField, Min(0f), Tooltip("Multiplies the light the sky casts on everything (ambient).")]
        float _ambientIntensity = 1f;

        [Header("Night")]
        [SerializeField, Tooltip("A moon's lit face.")]
        Color _moonColor = new Color(0.92f, 0.90f, 0.84f);

        [SerializeField, Range(0f, 1f), Tooltip("How bright a moon looks by day, against its brightness at night.")]
        float _moonByDay = 0.35f;

        [SerializeField, Tooltip("The light a moon casts once it outshines the sun.")]
        Color _moonLight = new Color(0.62f, 0.72f, 1f);

        [SerializeField, Tooltip("A full moon's light intensity by its height (in degrees); a half moon casts half of it.")]
        AnimationCurve _moonIntensity = new AnimationCurve(new Keyframe(-2f, 0f), new Keyframe(8f, 0.3f), new Keyframe(40f, 0.45f), new Keyframe(90f, 0.5f));

        [SerializeField, Min(0f), Tooltip("How bright the stars are once the sun is well down.")]
        float _starBrightness = 1.5f;

        public Material Material => _material;

        public float AmbientIntensity => _ambientIntensity;

        public Color MoonColor => _moonColor;

        public float MoonByDay => _moonByDay;

        public Color MoonLight => _moonLight;

        public float StarBrightness => _starBrightness;

        /// <summary>The light a full moon casts from <paramref name="moonHeight"/> degrees above the horizon.</summary>
        public float MoonIntensityAt(float moonHeight) => Mathf.Max(0f, _moonIntensity.Evaluate(moonHeight));

        /// <summary>The sky's colors with the sun <paramref name="sunHeight"/> degrees above the horizon.</summary>
        public SkyColors ColorsAt(float sunHeight)
        {
            var t = Mathf.InverseLerp(LowestSun, HighestSun, sunHeight);
            return new SkyColors(_zenith.Evaluate(t), _horizon.Evaluate(t), _ground.Evaluate(t), _sunLight.Evaluate(t),
                Mathf.Max(0f, _sunIntensity.Evaluate(sunHeight)));
        }

        // A gradient keyed by the sun's height in degrees.
        static Gradient Over(params (float SunHeight, Color Color)[] keys)
        {
            var colors = new GradientColorKey[keys.Length];
            for (var i = 0; i < keys.Length; i++)
                colors[i] = new GradientColorKey(keys[i].Color, Mathf.InverseLerp(LowestSun, HighestSun, keys[i].SunHeight));

            var gradient = new Gradient();
            gradient.SetKeys(colors, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
