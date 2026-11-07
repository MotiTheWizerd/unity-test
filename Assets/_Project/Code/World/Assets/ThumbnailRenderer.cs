using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Pictures of assets for the UI: each asset key is instantiated once through the same catalog the world uses,
    /// framed whole (<see cref="ThumbnailFraming"/>) and drawn by an orthographic camera in a <see cref="PreviewStudio"/>
    /// into a small texture with a transparent background, then kept for the session. Rendering happens on first
    /// request, so only shown tiles cost anything. Material balls come small, for tiles, or large and lit like a product
    /// shot (the key and the rim light), for a preview card.
    /// </summary>
    /// <remarks>The instance exists only inside the synchronous render, so no other camera ever sees it.</remarks>
    public sealed class ThumbnailRenderer : IAssetThumbnails, IDisposable
    {
        public const int Size = 128;
        public const int PreviewSize = 512;

        readonly IAssetCatalog _assets;
        readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        readonly Dictionary<int, RenderTexture> _targets = new Dictionary<int, RenderTexture>();
        readonly PreviewStudio _studio;

        public ThumbnailRenderer(IAssetCatalog assets, Transform parent)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _studio = new PreviewStudio("Thumbnails", parent);
            _studio.Camera.orthographic = true;
        }

        /// <summary>Where the camera stands to show <paramref name="bounds"/> whole (<see cref="ThumbnailFraming"/>, <see cref="IconFraming"/>).</summary>
        delegate Pose Framing(Bounds bounds, out float orthographicSize, out float nearPlane, out float farPlane);

        /// <summary>
        /// How a picture is taken: its size in pixels, whether the rim light joins the key light, how it is framed, and
        /// whether it keeps smaller copies of itself (mipmaps) for being shown much smaller than it was drawn.
        /// </summary>
        readonly struct Shot
        {
            public Shot(int size, bool rim, Framing framing, bool mipmaps)
            {
                Size = size;
                Rim = rim;
                Frame = framing;
                Mipmaps = mipmaps;
            }

            public int Size { get; }
            public bool Rim { get; }
            public Framing Frame { get; }
            public bool Mipmaps { get; }
        }

        static readonly Shot Tile = new Shot(Size, false, ThumbnailFraming.CameraPose, false);
        static readonly Shot Preview = new Shot(PreviewSize, true, ThumbnailFraming.CameraPose, false);
        static readonly Shot Icon = new Shot(Size, true, IconFraming.CameraPose, true);

        /// <summary>The picture for <paramref name="assetKey"/>, rendered on first request.</summary>
        public Texture2D Get(string assetKey) => Get(assetKey, null, null);

        /// <summary>
        /// An icon of <paramref name="assetKey"/> for a list (<see cref="IconFraming"/>: its telling face, a long thing on
        /// the diagonal, lit like a product shot), rendered on first request.
        /// </summary>
        public Texture2D GetIcon(string assetKey)
        {
            if (string.IsNullOrEmpty(assetKey)) throw new ArgumentException("Asset key is required.", nameof(assetKey));
            return Cached(IconPrefix + assetKey, Icon, () => _assets.Instantiate(assetKey, _studio.Stage));
        }

        /// <summary>The picture for <paramref name="assetKey"/> dressed by <paramref name="dress"/>, rendered on first request.</summary>
        public Texture2D Get(string assetKey, string lookKey, Action<GameObject> dress)
        {
            if (string.IsNullOrEmpty(assetKey)) throw new ArgumentException("Asset key is required.", nameof(assetKey));
            if (string.IsNullOrEmpty(lookKey) || dress == null)
                return Cached(assetKey, Tile, () => _assets.Instantiate(assetKey, _studio.Stage));

            return Cached(assetKey + LookSeparator + lookKey, Tile, () =>
            {
                var instance = _assets.Instantiate(assetKey, _studio.Stage);
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

        // Balls and icons share the cache with assets; the prefixes keep a ball, its preview, an icon and an asset of the
        // same key apart.
        const string BallPrefix = "ball:";
        const string PreviewPrefix = "preview:";
        const string IconPrefix = "icon:";

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
            ball.transform.SetParent(_studio.Stage, false);
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
            var instance = stage();
            try
            {
                _studio.PutOnLayer(instance);
                var camera = _studio.Camera;
                var pose = shot.Frame(PreviewStudio.BoundsOf(instance), out var size, out var near, out var far);
                camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                camera.orthographicSize = size;
                camera.nearClipPlane = near;
                camera.farClipPlane = far;
                _studio.Render(target, shot.Rim);
                return ReadBack(key, target, shot.Mipmaps);
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

        static Texture2D ReadBack(string key, RenderTexture target, bool mipmaps)
        {
            var size = target.width;
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipmaps) { name = $"Thumbnail {key}" };
            if (mipmaps)
                texture.filterMode = FilterMode.Trilinear;
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            texture.Apply(mipmaps, true);
            RenderTexture.active = previous;
            return texture;
        }

        public void Dispose()
        {
            foreach (var texture in _cache.Values)
                UnityEngine.Object.Destroy(texture);
            _cache.Clear();
            _studio.Dispose();
            foreach (var target in _targets.Values)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }

            _targets.Clear();
        }
    }
}
