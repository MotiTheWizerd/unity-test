using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.World.Sky
{
    /// <summary>
    /// Puts a sky in the scene from a <see cref="SkyProfile"/> and the sun's position: the skybox, the sun's light,
    /// the fog (the horizon's color, thick at the edge of the streamed land so it fades out instead of ending), the
    /// ambient light from the sky, and a reflection of the sky that water and shiny things pick up. Call
    /// <see cref="Show"/> whenever the sun moves; it is not cheap enough for every frame (the ambient light and the
    /// reflection are re-rendered), so a moving clock calls it a few times per game hour.
    /// </summary>
    public sealed class SkyView : IDisposable
    {
        // Exponential squared fog lets through exp(-(d·density)²); 1.73 leaves 5% at the reach.
        const float FogAtReach = 1.73f;

        readonly SkyProfile _profile;
        readonly Light _sun;
        readonly Material _material;
        readonly ReflectionProbe _reflection;
        readonly float _fogDensity;

        /// <param name="sun">The scene's directional light, which becomes the sun.</param>
        /// <param name="fogReach">Distance in meters at which the fog hides everything: the edge of the streamed land.</param>
        public SkyView(SkyProfile profile, Light sun, Transform parent, float fogReach)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _sun = sun != null ? sun : throw new ArgumentNullException(nameof(sun));
            if (profile.Material == null)
                throw new ArgumentException($"Sky profile '{profile.name}' has no material.", nameof(profile));
            if (!(fogReach > 0f))
                throw new ArgumentOutOfRangeException(nameof(fogReach), fogReach, "The fog's reach must be positive.");

            _material = new Material(profile.Material) { name = "Sky (runtime)" };
            _fogDensity = FogAtReach / fogReach;
            _reflection = CreateReflection(parent);
        }

        /// <summary>Shows the sky with the sun where the profile puts it (until the world clock moves it).</summary>
        public void ShowProfileSun() => Show(_profile.SunHeight, _profile.SunBearing);

        /// <summary>Shows the sky with the sun <paramref name="height"/> degrees above the horizon, toward compass <paramref name="bearing"/>.</summary>
        public void Show(float height, float bearing)
        {
            var toSun = SunDirection(height, bearing);
            var colors = _profile.ColorsAt(height);

            _material.SetColor("_ZenithColor", colors.Zenith);
            _material.SetColor("_HorizonColor", colors.Horizon);
            _material.SetColor("_GroundColor", colors.Ground);
            _material.SetColor("_SunColor", colors.SunLight);
            _material.SetVector("_SunDirection", toSun);
            RenderSettings.skybox = _material;

            _sun.transform.rotation = Quaternion.LookRotation(-toSun);
            _sun.color = colors.SunLight;
            _sun.intensity = colors.SunIntensity;
            _sun.enabled = colors.SunIntensity > 0f;
            RenderSettings.sun = _sun;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.fogColor = colors.Horizon;

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = _profile.AmbientIntensity;
            DynamicGI.UpdateEnvironment();
            _reflection.RenderProbe();
        }

        /// <summary>The direction toward the sun: <paramref name="height"/> degrees up, compass <paramref name="bearing"/> (0 north = +z, 90 east = +x).</summary>
        public static Vector3 SunDirection(float height, float bearing)
        {
            var up = height * Mathf.Deg2Rad;
            var around = bearing * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(up) * Mathf.Sin(around), Mathf.Sin(up), Mathf.Cos(up) * Mathf.Cos(around));
        }

        public void Dispose()
        {
            if (_reflection != null)
                UnityEngine.Object.Destroy(_reflection.gameObject);
            UnityEngine.Object.Destroy(_material);
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
