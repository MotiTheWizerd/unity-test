using System;
using System.Collections.Generic;
using Forge.Data.Sky;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.World.Sky
{
    /// <summary>
    /// Puts a sky in the scene from a <see cref="SkyProfile"/> and a <see cref="SkyState"/>: the skybox (with the sun,
    /// the moons and the stars), the sky's light, the fog (the horizon's color, thick at the edge of the streamed land
    /// so it fades out instead of ending), the ambient light from the sky, and a reflection of the sky that water and
    /// shiny things pick up. The sky's light is the sun's while it outshines the brightest moon, then that moon's: one
    /// shadowed light, swapped at dusk and dawn when both are faint (as Skyrim does). Call <see cref="Show"/> whenever
    /// the sky moves; it is not cheap enough for every frame (the ambient light and the reflection are re-rendered), so
    /// a moving clock calls it a few times per game hour.
    /// </summary>
    public sealed class SkyView : IDisposable
    {
        // Exponential squared fog lets through exp(-(d·density)²); 1.73 leaves 5% at the reach.
        const float FogAtReach = 1.73f;

        // How far below the horizon the sun's glow still reaches the sky, in degrees.
        const float TwilightDepth = 8f;

        // The stars come out as the sun sinks from the first height to the second, in degrees.
        const float StarsAppear = -3f;
        const float StarsFull = -12f;

        // The moon looks dim by day and bright by night as the sun climbs from the first height to the second.
        const float MoonDimFrom = -6f;
        const float MoonDimTo = 10f;

        // The stars turn around the pole star, due north this far above the horizon.
        const float PoleHeight = 50f;

        static readonly Vector3 Pole = Direction(new SkyPosition(PoleHeight, 0f));

        readonly SkyProfile _profile;
        readonly Light _light;
        readonly Material _material;
        readonly ReflectionProbe _reflection;
        readonly float _fogDensity;
        readonly Vector4[] _moonDirections = new Vector4[Moon.MaxPerSky];
        readonly Vector4[] _moonShapes = new Vector4[Moon.MaxPerSky];

        /// <param name="light">The scene's directional light, which becomes the sun's (and by night the moon's) light.</param>
        /// <param name="fogReach">Distance in meters at which the fog hides everything: the edge of the streamed land.</param>
        public SkyView(SkyProfile profile, Light light, Transform parent, float fogReach)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _light = light != null ? light : throw new ArgumentNullException(nameof(light));
            if (profile.Material == null)
                throw new ArgumentException($"Sky profile '{profile.name}' has no material.", nameof(profile));
            if (!(fogReach > 0f))
                throw new ArgumentOutOfRangeException(nameof(fogReach), fogReach, "The fog's reach must be positive.");

            _material = new Material(profile.Material) { name = "Sky (runtime)" };
            _fogDensity = FogAtReach / fogReach;
            _reflection = CreateReflection(parent);
        }

        /// <summary>Shows the sky as it is in <paramref name="sky"/>.</summary>
        public void Show(SkyState sky)
        {
            if (sky == null)
                throw new ArgumentNullException(nameof(sky));
            if (sky.Moons.Count > Moon.MaxPerSky)
                throw new ArgumentException($"The sky draws at most {Moon.MaxPerSky} moons, not {sky.Moons.Count}.", nameof(sky));

            var sun = sky.Sun;
            var toSun = Direction(sun);
            var colors = _profile.ColorsAt(sun.Height);

            _material.SetColor("_ZenithColor", colors.Zenith);
            _material.SetColor("_HorizonColor", colors.Horizon);
            _material.SetColor("_GroundColor", colors.Ground);
            // The disk, its glow and the horizon haze linger through twilight, then fade out under the horizon.
            _material.SetColor("_SunColor", colors.SunLight * Mathf.Clamp01((sun.Height + TwilightDepth) / TwilightDepth));
            _material.SetVector("_SunDirection", toSun);
            ShowMoons(sky.Moons, sun.Height);
            _material.SetFloat("_StarBrightness", _profile.StarBrightness * Mathf.InverseLerp(StarsAppear, StarsFull, sun.Height));
            // The shader looks the stars up in the sky's own frame, so it gets the turn undone.
            _material.SetMatrix("_StarRotation", Matrix4x4.Rotate(Quaternion.AngleAxis(-sky.StarTurn, Pole)));
            RenderSettings.skybox = _material;

            ShowLight(toSun, colors, sky.Moons);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.fogColor = colors.Horizon;

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = _profile.AmbientIntensity;
            DynamicGI.UpdateEnvironment();
            _reflection.RenderProbe();
        }

        /// <summary>The direction toward a point in the sky: its height up, toward its compass bearing (0 north = +z, 90 east = +x).</summary>
        public static Vector3 Direction(SkyPosition position)
        {
            var up = position.Height * Mathf.Deg2Rad;
            var around = position.Bearing * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(up) * Mathf.Sin(around), Mathf.Sin(up), Mathf.Cos(up) * Mathf.Cos(around));
        }

        public void Dispose()
        {
            if (_reflection != null)
                UnityEngine.Object.Destroy(_reflection.gameObject);
            UnityEngine.Object.Destroy(_material);
        }

        // Each moon's direction and its size, lit share and a seed for its markings; the shader lights its face from the sun.
        void ShowMoons(IReadOnlyList<MoonState> moons, float sunHeight)
        {
            for (var i = 0; i < Moon.MaxPerSky; i++)
            {
                var shown = i < moons.Count;
                _moonDirections[i] = shown ? (Vector4)Direction(moons[i].Position) : Vector4.zero;
                _moonShapes[i] = shown ? new Vector4(moons[i].Moon.Size, moons[i].Lit, 17.3f * (i + 1), 0f) : Vector4.zero;
            }

            _material.SetVectorArray("_MoonDirections", _moonDirections);
            _material.SetVectorArray("_MoonShapes", _moonShapes);
            _material.SetFloat("_MoonCount", moons.Count);
            var byDay = Mathf.Lerp(1f, _profile.MoonByDay, Mathf.InverseLerp(MoonDimFrom, MoonDimTo, sunHeight));
            _material.SetColor("_MoonColor", _profile.MoonColor * byDay);
        }

        // The sun's light, or the brightest moon's once it outshines the sun.
        void ShowLight(Vector3 toSun, SkyColors colors, IReadOnlyList<MoonState> moons)
        {
            var from = toSun;
            var color = colors.SunLight;
            var intensity = colors.SunIntensity;
            foreach (var moon in moons)
            {
                var moonlight = _profile.MoonIntensityAt(moon.Position.Height) * moon.Lit;
                if (moonlight <= intensity)
                    continue;

                from = Direction(moon.Position);
                color = _profile.MoonLight;
                intensity = moonlight;
            }

            _light.transform.rotation = Quaternion.LookRotation(-from);
            _light.color = color;
            _light.intensity = intensity;
            _light.enabled = intensity > 0f;
            RenderSettings.sun = _light;
        }

        // A reflection of the sky alone (it sees no objects), covering the whole world, rendered when the sky changes.
        static ReflectionProbe CreateReflection(Transform parent)
        {
            var go = new GameObject("Sky Reflection");
            go.transform.SetParent(parent, false);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = 0;
            probe.size = Vector3.one * 100000f;
            probe.resolution = 128;
            probe.hdr = true;
            return probe;
        }
    }
}
