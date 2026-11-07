using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.World.Assets
{
    /// <summary>
    /// A small photo studio for pictures of the world's things, apart from the world: a camera that draws only the
    /// ForgePreview layer, a key light and a rim light, and a stage to stand things on. Whatever takes pictures with it
    /// (still thumbnails, a live turntable) sets the camera up, puts its subject on the <see cref="Stage"/> and calls
    /// <see cref="Render"/>. The camera never renders by itself, and the lighting is the studio's own: for each render the
    /// scene's lights are switched off and the ambient is flat, so a picture looks the same at noon and at midnight.
    /// </summary>
    public sealed class PreviewStudio : IDisposable
    {
        public const string LayerName = "ForgePreview";

        static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        static readonly Color Ambient = new Color(0.35f, 0.35f, 0.38f);

        readonly GameObject _rig;
        readonly Light _key;
        readonly Light _rim;

        /// <param name="name">The rig's name in the scene ("Thumbnails").</param>
        /// <param name="parent">What the rig hangs under; null for the scene's root.</param>
        public PreviewStudio(string name, Transform parent)
        {
            Layer = LayerMask.NameToLayer(LayerName);
            if (Layer < 0)
                Debug.LogError($"[Forge] Layer '{LayerName}' is missing from the project's tag manager; previews will show the world behind them.");

            _rig = new GameObject(name);
            _rig.transform.SetParent(parent, false);

            Camera = new GameObject("Camera").AddComponent<Camera>();
            Camera.transform.SetParent(_rig.transform, false);
            Camera.enabled = false; // renders only on request
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Transparent;
            Camera.cullingMask = Layer >= 0 ? 1 << Layer : ~0;
            Camera.allowHDR = false;
            Camera.allowMSAA = false;
            Camera.useOcclusionCulling = false;

            _key = CreateLight("Key", Quaternion.Euler(40f, -30f, 0f), 1.1f, Color.white);
            // From behind and above: a cool bright edge that lifts the subject off its backdrop.
            _rim = CreateLight("Rim", Quaternion.Euler(25f, 150f, 0f), 0.9f, new Color(0.85f, 0.9f, 1f));

            Stage = new GameObject("Stage").transform;
            Stage.SetParent(_rig.transform, false);
        }

        /// <summary>The studio's camera, set up by whoever takes the picture; it renders only in <see cref="Render"/>.</summary>
        public Camera Camera { get; }

        /// <summary>Where subjects stand; a subject's parts must be on <see cref="Layer"/> (<see cref="PutOnLayer"/>).</summary>
        public Transform Stage { get; }

        /// <summary>The ForgePreview layer, or -1 when the project lacks it.</summary>
        public int Layer { get; }

        /// <summary>
        /// Draws the stage into <paramref name="target"/> under the studio's key light, and its rim light too when
        /// <paramref name="rim"/>.
        /// </summary>
        public void Render(RenderTexture target, bool rim)
        {
            Camera.targetTexture = target;
            using (new StudioLighting(_key, rim ? _rim : null, Ambient))
            using (SyncShaderCompilation.Begin())
                Camera.Render();
        }

        /// <summary>Puts <paramref name="subject"/> and all its parts on the studio's layer, so only its camera sees them.</summary>
        public void PutOnLayer(GameObject subject)
        {
            if (Layer >= 0)
                SetLayerRecursively(subject.transform, Layer);
        }

        /// <summary>
        /// The world bounds of everything <paramref name="subject"/> draws, a character as it stands now; a unit box at it
        /// when it draws nothing.
        /// </summary>
        public static Bounds BoundsOf(GameObject subject) =>
            RendererBounds.TryGetPosed(subject, out var bounds) ? bounds : new Bounds(subject.transform.position, Vector3.one);

        public void Dispose()
        {
            if (Camera != null)
                Camera.targetTexture = null;
            if (_rig != null)
                UnityEngine.Object.Destroy(_rig);
        }

        // A directional light of the rig, lighting only what the camera draws; on only while rendering.
        Light CreateLight(string name, Quaternion rotation, float intensity, Color color)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(_rig.transform, false);
            light.transform.rotation = rotation;
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            light.cullingMask = Camera.cullingMask;
            light.enabled = false;
            return light;
        }

        static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        /// <summary>
        /// For one render, in the Unity editor: shaders compile before drawing instead of in the background, so a still
        /// picture never keeps the editor's cyan stand-in for a shader still compiling. Builds compile shaders ahead, so
        /// this does nothing there.
        /// </summary>
        readonly struct SyncShaderCompilation : IDisposable
        {
#if UNITY_EDITOR
            readonly bool _wasAsync;

            SyncShaderCompilation(bool wasAsync) => _wasAsync = wasAsync;

            public static SyncShaderCompilation Begin()
            {
                var scope = new SyncShaderCompilation(UnityEditor.ShaderUtil.allowAsyncCompilation);
                UnityEditor.ShaderUtil.allowAsyncCompilation = false;
                return scope;
            }

            public void Dispose() => UnityEditor.ShaderUtil.allowAsyncCompilation = _wasAsync;
#else
            public static SyncShaderCompilation Begin() => default;

            public void Dispose() { }
#endif
        }

        /// <summary>
        /// For one render: the studio's lights on (the key light, and the rim light when given), every other light off,
        /// and a flat ambient instead of the sky's. Restores everything after.
        /// </summary>
        readonly struct StudioLighting : IDisposable
        {
            readonly Light _key;
            readonly Light _rim;
            readonly List<Light> _silenced;
            readonly AmbientMode _ambientMode;
            readonly Color _ambientColor;

            public StudioLighting(Light key, Light rim, Color ambient)
            {
                _key = key;
                _rim = rim;
                _silenced = new List<Light>();
                foreach (var light in UnityEngine.Object.FindObjectsByType<Light>())
                {
                    if (light != key && light != rim && light.enabled)
                    {
                        light.enabled = false;
                        _silenced.Add(light);
                    }
                }

                _ambientMode = RenderSettings.ambientMode;
                _ambientColor = RenderSettings.ambientLight;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambient;
                key.enabled = true;
                if (rim != null)
                    rim.enabled = true;
            }

            public void Dispose()
            {
                _key.enabled = false;
                if (_rim != null)
                    _rim.enabled = false;
                foreach (var light in _silenced)
                    light.enabled = true;
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientLight = _ambientColor;
            }
        }
    }
}
