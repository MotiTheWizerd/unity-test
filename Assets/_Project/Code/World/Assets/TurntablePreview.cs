using System;
using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// A thing of the world shown live, turning slowly on a turntable in a <see cref="PreviewStudio"/>, as Skyrim shows the
    /// item chosen in its inventory: built from the same asset the world uses (<see cref="Show"/>), framed whole by its
    /// size (<see cref="TurntableFraming"/>), so a coin and a sword both fill the view, and drawn every
    /// <see cref="Tick"/> into <see cref="Texture"/> on a transparent background for a UI to show. Whoever shows it can
    /// turn it by hand (<see cref="Turn"/>, holding it still meanwhile with <see cref="Held"/>) and zoom it
    /// (<see cref="Zoom"/>). The subject is active only inside its own render, so no other camera ever sees it.
    /// </summary>
    public sealed class TurntablePreview : IDisposable
    {
        // A long lens flattens less than a wide one: things look as they do in the hand.
        const float FieldOfView = 30f;

        /// <summary>Degrees a second the turntable turns by itself.</summary>
        public const float TurnSpeed = 25f;

        // The turn a new subject starts at: three-quarters on, its front (+Z) towards the camera's left.
        const float StartYaw = 210f;

        // Each wheel notch zooms by this factor.
        const float ZoomStep = 1.15f;

        // The largest texture side, whatever the UI asks.
        const int MaxSide = 2048;

        readonly IAssetCatalog _assets;
        readonly PreviewStudio _studio;
        readonly Transform _turntable;
        GameObject _subject;
        RenderTexture _target;
        float _reach;
        float _halfHeight;
        float _yaw;
        float _zoom = 1f;

        public TurntablePreview(IAssetCatalog assets)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _studio = new PreviewStudio("Turntable", null);
            var camera = _studio.Camera;
            camera.orthographic = false;
            camera.fieldOfView = FieldOfView;
            camera.allowMSAA = true;
            _turntable = new GameObject("Turntable").transform;
            _turntable.SetParent(_studio.Stage, false);
        }

        /// <summary>The picture, redrawn each <see cref="Tick"/>; null until <see cref="Resize"/> gives it a size.</summary>
        public RenderTexture Texture => _target;

        /// <summary>The asset key on the turntable, or null for nothing.</summary>
        public string Shown { get; private set; }

        /// <summary>Held by hand: it doesn't turn by itself.</summary>
        public bool Held { get; set; }

        /// <summary>
        /// Puts <paramref name="assetKey"/>'s asset on the turntable, turned three-quarters on and zoomed to fit; null
        /// clears it. The same key again keeps the turn and the zoom.
        /// </summary>
        public void Show(string assetKey)
        {
            if (string.Equals(assetKey, Shown, StringComparison.Ordinal))
                return;

            Clear();
            Shown = assetKey;
            if (string.IsNullOrEmpty(assetKey))
                return;

            _turntable.localRotation = Quaternion.identity;
            _subject = _assets.Instantiate(assetKey, _turntable);
            _studio.PutOnLayer(_subject);
            // Its drawn centre sits on the turntable's axis, so it turns in place.
            var bounds = PreviewStudio.BoundsOf(_subject);
            _subject.transform.localPosition -= _turntable.InverseTransformPoint(bounds.center);
            // Turning, it sweeps a cylinder: this far around its axis, this far up and down.
            _reach = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
            _halfHeight = bounds.extents.y;
            _subject.SetActive(false);
            _yaw = StartYaw;
            _zoom = 1f;
        }

        /// <summary>Draws at <paramref name="width"/> × <paramref name="height"/> pixels from now on (the UI's size).</summary>
        public void Resize(int width, int height)
        {
            width = Mathf.Clamp(width, 1, MaxSide);
            height = Mathf.Clamp(height, 1, MaxSide);
            if (_target != null && _target.width == width && _target.height == height)
                return;

            ReleaseTarget();
            _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Turntable", antiAliasing = 4 };
        }

        /// <summary>Turns the subject by <paramref name="degrees"/> (a drag).</summary>
        public void Turn(float degrees) => _yaw += degrees;

        /// <summary>Zooms in by <paramref name="notches"/> (out when negative), within <see cref="TurntableFraming"/>'s bounds.</summary>
        public void Zoom(float notches) =>
            _zoom = Mathf.Clamp(_zoom * Mathf.Pow(ZoomStep, notches), TurntableFraming.MinZoom, TurntableFraming.MaxZoom);

        /// <summary>Turns <paramref name="deltaTime"/> seconds' worth unless held, and draws the picture.</summary>
        public void Tick(float deltaTime)
        {
            if (_subject == null || _target == null)
                return;

            if (!Held)
                _yaw += TurnSpeed * deltaTime;
            _yaw = Mathf.Repeat(_yaw, 360f);
            _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);

            var camera = _studio.Camera;
            var distance = TurntableFraming.Distance(_reach, _halfHeight, FieldOfView, (float)_target.width / _target.height, _zoom);
            var pose = TurntableFraming.CameraPose(_turntable.position, distance);
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var radius = new Vector2(_reach, _halfHeight).magnitude;
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            camera.farClipPlane = distance + radius * 2f;

            _subject.SetActive(true);
            try
            {
                _studio.Render(_target, true);
            }
            finally
            {
                _subject.SetActive(false);
            }
        }

        public void Dispose()
        {
            Clear();
            _studio.Dispose();
            ReleaseTarget();
        }

        void Clear()
        {
            if (_subject != null)
                UnityEngine.Object.Destroy(_subject);
            _subject = null;
            Shown = null;
        }

        void ReleaseTarget()
        {
            if (_target == null)
                return;
            _target.Release();
            UnityEngine.Object.Destroy(_target);
            _target = null;
        }
    }
}
