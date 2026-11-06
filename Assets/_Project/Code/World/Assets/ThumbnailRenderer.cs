using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.World.Assets
{
    /// <summary>
    /// Pictures of assets for the UI: each asset key is instantiated once through the same catalog the world uses,
    /// framed whole (<see cref="ThumbnailFraming"/>) and drawn by a private camera into a small texture with a
    /// transparent background, then kept for the session. Rendering happens on first request, so only shown tiles
    /// cost anything.
    /// </summary>
    /// <remarks>
    /// The rig lives on the ForgePreview layer so the camera draws nothing of the world, and the instance exists
    /// only inside the synchronous render, so no other camera ever sees it. Lighting is the rig's own: the scene's
    /// lights are switched off for the one render so a picture looks the same at noon and at midnight.
    /// </remarks>
    public sealed class ThumbnailRenderer : IAssetThumbnails, IDisposable
    {
        public const string LayerName = "ForgePreview";
        public const int Size = 128;

        static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        static readonly Color Ambient = new Color(0.35f, 0.35f, 0.38f);

        readonly IAssetCatalog _assets;
        readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        readonly GameObject _rig;
        readonly Camera _camera;
        readonly Transform _stage;
        readonly Light _light;
        readonly RenderTexture _target;
        readonly int _layer;

        public ThumbnailRenderer(IAssetCatalog assets, Transform parent)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _layer = LayerMask.NameToLayer(LayerName);
            if (_layer < 0)
                Debug.LogError($"[Forge] Layer '{LayerName}' is missing from the project's tag manager; asset thumbnails will show the world behind them.");

            _rig = new GameObject("Thumbnails");
            _rig.transform.SetParent(parent, false);

            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_rig.transform, false);
            _camera.enabled = false; // renders only on request
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Transparent;
            _camera.cullingMask = _layer >= 0 ? 1 << _layer : ~0;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;

            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(_rig.transform, false);
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            light.cullingMask = _camera.cullingMask;
            light.enabled = false; // on only while rendering
            _light = light;

            _stage = new GameObject("Stage").transform;
            _stage.SetParent(_rig.transform, false);

            _target = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { name = "Thumbnail" };
            _camera.targetTexture = _target;
        }

        /// <summary>The picture for <paramref name="assetKey"/>, rendered on first request.</summary>
        public Texture2D Get(string assetKey) => Get(assetKey, null, null);

        /// <summary>The picture for <paramref name="assetKey"/> dressed by <paramref name="dress"/>, rendered on first request.</summary>
        public Texture2D Get(string assetKey, string lookKey, Action<GameObject> dress)
        {
            if (string.IsNullOrEmpty(assetKey)) throw new ArgumentException("Asset key is required.", nameof(assetKey));
            if (string.IsNullOrEmpty(lookKey) || dress == null)
                return Cached(assetKey, () => _assets.Instantiate(assetKey, _stage));

            return Cached(assetKey + LookSeparator + lookKey, () =>
            {
                var instance = _assets.Instantiate(assetKey, _stage);
                dress(instance);
                return instance;
            });
        }

        // Asset keys never hold it, so a dressed look can't collide with a plain asset's picture.
        const string LookSeparator = "|";

        /// <summary>
        /// A material ball: a sphere wearing the material <paramref name="createMaterial"/> makes, rendered on first
        /// request for <paramref name="key"/>. The material is made only then, and destroyed once the picture is taken.
        /// </summary>
        public Texture2D GetBall(string key, Func<Material> createMaterial)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key is required.", nameof(key));
            if (createMaterial == null) throw new ArgumentNullException(nameof(createMaterial));

            Material material = null;
            try
            {
                return Cached(BallPrefix + key, () =>
                {
                    material = createMaterial();
                    return Ball(material);
                });
            }
            finally
            {
                if (material != null)
                    UnityEngine.Object.DestroyImmediate(material);
            }
        }

        // Balls share the cache with assets; the prefix keeps a ball and an asset of the same key apart.
        const string BallPrefix = "ball:";

        /// <summary>Texture repeats around and over the ball, so a ground texture shows at about its real scale.</summary>
        static readonly Vector2 BallTiling = new Vector2(2f, 1f);

        GameObject Ball(Material material)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.SetParent(_stage, false);
            if (material != null)
            {
                material.mainTextureScale = BallTiling;
                ball.GetComponent<Renderer>().sharedMaterial = material;
            }

            return ball;
        }

        Texture2D Cached(string key, Func<GameObject> stage)
        {
            if (!_cache.TryGetValue(key, out var texture))
            {
                texture = Render(key, stage);
                _cache.Add(key, texture);
            }

            return texture;
        }

        Texture2D Render(string key, Func<GameObject> stage)
        {
            var instance = stage();
            try
            {
                SetLayerRecursively(instance.transform, _layer);
                var pose = ThumbnailFraming.CameraPose(BoundsOf(instance), out var size, out var near, out var far);
                _camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                _camera.orthographicSize = size;
                _camera.nearClipPlane = near;
                _camera.farClipPlane = far;

                using (new PreviewLighting(_light, Ambient))
                using (SyncShaderCompilation.Begin())
                    _camera.Render();

                return ReadBack(key);
            }
            finally
            {
                // Immediately: the next picture may be rendered this same frame, and the stage must be empty by then.
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        Texture2D ReadBack(string key)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = $"Thumbnail {key}" };
            texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            texture.Apply(false, true);
            RenderTexture.active = previous;
            return texture;
        }

        static Bounds BoundsOf(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(instance.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static void SetLayerRecursively(Transform root, int layer)
        {
            if (layer < 0)
                return;
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        public void Dispose()
        {
            foreach (var texture in _cache.Values)
                UnityEngine.Object.Destroy(texture);
            _cache.Clear();
            if (_target != null)
                _target.Release();
            UnityEngine.Object.Destroy(_target);
            if (_rig != null)
                UnityEngine.Object.Destroy(_rig);
        }

        /// <summary>
        /// For one render, in the Unity editor: shaders compile before drawing instead of in the background. A picture
        /// is taken once and kept, so drawing with the editor's cyan stand-in for a still-compiling shader would keep
        /// that for the session. Builds compile shaders ahead, so this does nothing there.
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
        /// For one render: the rig's light on, every other light off, and a flat ambient instead of the sky's.
        /// Restores everything after.
        /// </summary>
        readonly struct PreviewLighting : IDisposable
        {
            readonly Light _own;
            readonly List<Light> _silenced;
            readonly AmbientMode _ambientMode;
            readonly Color _ambientColor;

            public PreviewLighting(Light own, Color ambient)
            {
                _own = own;
                _silenced = new List<Light>();
                foreach (var light in UnityEngine.Object.FindObjectsByType<Light>())
                {
                    if (light != own && light.enabled)
                    {
                        light.enabled = false;
                        _silenced.Add(light);
                    }
                }

                _ambientMode = RenderSettings.ambientMode;
                _ambientColor = RenderSettings.ambientLight;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambient;
                own.enabled = true;
            }

            public void Dispose()
            {
                _own.enabled = false;
                foreach (var light in _silenced)
                    light.enabled = true;
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientLight = _ambientColor;
            }
        }
    }
}
