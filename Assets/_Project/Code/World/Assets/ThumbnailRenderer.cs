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
    /// cost anything. Material balls come small, for tiles, or large and lit like a product shot (a key light and a rim
    /// light), for a preview card.
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
        public const int PreviewSize = 512;

        static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        static readonly Color Ambient = new Color(0.35f, 0.35f, 0.38f);

        readonly IAssetCatalog _assets;
        readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        readonly Dictionary<int, RenderTexture> _targets = new Dictionary<int, RenderTexture>();
        readonly GameObject _rig;
        readonly Camera _camera;
        readonly Transform _stage;
        readonly Light _light;
        readonly Light _rim;
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

            _light = CreateLight("Light", Quaternion.Euler(40f, -30f, 0f), 1.1f, Color.white);
            // From behind and above, for previews: a cool bright edge that lifts the ball off its backdrop.
            _rim = CreateLight("Rim", Quaternion.Euler(25f, 150f, 0f), 0.9f, new Color(0.85f, 0.9f, 1f));

            _stage = new GameObject("Stage").transform;
            _stage.SetParent(_rig.transform, false);
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
            light.cullingMask = _camera.cullingMask;
            light.enabled = false;
            return light;
        }

        /// <summary>How a picture is taken: its size in pixels, and whether the rim light joins the key light.</summary>
        readonly struct Shot
        {
            public Shot(int size, bool rim)
            {
                Size = size;
                Rim = rim;
            }

            public int Size { get; }
            public bool Rim { get; }
        }

        static readonly Shot Tile = new Shot(Size, false);
        static readonly Shot Preview = new Shot(PreviewSize, true);

        /// <summary>The picture for <paramref name="assetKey"/>, rendered on first request.</summary>
        public Texture2D Get(string assetKey) => Get(assetKey, null, null);

        /// <summary>The picture for <paramref name="assetKey"/> dressed by <paramref name="dress"/>, rendered on first request.</summary>
        public Texture2D Get(string assetKey, string lookKey, Action<GameObject> dress)
        {
            if (string.IsNullOrEmpty(assetKey)) throw new ArgumentException("Asset key is required.", nameof(assetKey));
            if (string.IsNullOrEmpty(lookKey) || dress == null)
                return Cached(assetKey, Tile, () => _assets.Instantiate(assetKey, _stage));

            return Cached(assetKey + LookSeparator + lookKey, Tile, () =>
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
        public Texture2D GetBall(string key, Func<Material> createMaterial) => Ball(BallPrefix, key, Tile, createMaterial);

        /// <summary>
        /// A large material ball for a preview card (<see cref="PreviewSize"/>, key and rim light) on a transparent
        /// background; otherwise as <see cref="GetBall"/>.
        /// </summary>
        public Texture2D GetBallPreview(string key, Func<Material> createMaterial) => Ball(PreviewPrefix, key, Preview, createMaterial);

        // Balls share the cache with assets; the prefixes keep a ball, its preview and an asset of the same key apart.
        const string BallPrefix = "ball:";
        const string PreviewPrefix = "preview:";

        Texture2D Ball(string prefix, string key, Shot shot, Func<Material> createMaterial)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key is required.", nameof(key));
            if (createMaterial == null) throw new ArgumentNullException(nameof(createMaterial));

            Material material = null;
            try
            {
                return Cached(prefix + key, shot, () =>
                {
                    material = createMaterial();
                    return CreateBall(material);
                });
            }
            finally
            {
                if (material != null)
                    UnityEngine.Object.DestroyImmediate(material);
            }
        }

        /// <summary>Texture repeats around and over the ball, so a ground texture shows at about its real scale.</summary>
        static readonly Vector2 BallTiling = new Vector2(2f, 1f);

        GameObject CreateBall(Material material)
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

        Texture2D Cached(string key, Shot shot, Func<GameObject> stage)
        {
            if (!_cache.TryGetValue(key, out var texture))
            {
                texture = Render(key, shot, stage);
                _cache.Add(key, texture);
            }

            return texture;
        }

        Texture2D Render(string key, Shot shot, Func<GameObject> stage)
        {
            var target = TargetOf(shot.Size);
            _camera.targetTexture = target;
            var instance = stage();
            try
            {
                SetLayerRecursively(instance.transform, _layer);
                var pose = ThumbnailFraming.CameraPose(BoundsOf(instance), out var size, out var near, out var far);
                _camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                _camera.orthographicSize = size;
                _camera.nearClipPlane = near;
                _camera.farClipPlane = far;

                using (new PreviewLighting(_light, shot.Rim ? _rim : null, Ambient))
                using (SyncShaderCompilation.Begin())
                    _camera.Render();

                return ReadBack(key, target);
            }
            finally
            {
                // Immediately: the next picture may be rendered this same frame, and the stage must be empty by then.
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        // One target per picture size, made when first needed.
        RenderTexture TargetOf(int size)
        {
            if (!_targets.TryGetValue(size, out var target))
            {
                target = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32) { name = $"Thumbnail {size}" };
                _targets.Add(size, target);
            }

            return target;
        }

        static Texture2D ReadBack(string key, RenderTexture target)
        {
            var size = target.width;
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = $"Thumbnail {key}" };
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
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
            if (_camera != null)
                _camera.targetTexture = null;
            foreach (var target in _targets.Values)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }

            _targets.Clear();
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
        /// For one render: the rig's lights on (the key light, and the rim light when given), every other light off,
        /// and a flat ambient instead of the sky's. Restores everything after.
        /// </summary>
        readonly struct PreviewLighting : IDisposable
        {
            readonly Light _own;
            readonly Light _rim;
            readonly List<Light> _silenced;
            readonly AmbientMode _ambientMode;
            readonly Color _ambientColor;

            public PreviewLighting(Light own, Light rim, Color ambient)
            {
                _own = own;
                _rim = rim;
                _silenced = new List<Light>();
                foreach (var light in UnityEngine.Object.FindObjectsByType<Light>())
                {
                    if (light != own && light != rim && light.enabled)
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
                if (rim != null)
                    rim.enabled = true;
            }

            public void Dispose()
            {
                _own.enabled = false;
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
