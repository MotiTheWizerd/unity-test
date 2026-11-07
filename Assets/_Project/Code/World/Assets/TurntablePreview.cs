using System;
using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// A thing of the world shown live in a <see cref="PreviewStudio"/>, as Skyrim shows the item chosen in its inventory:
    /// built from the same asset the world uses (<see cref="Show"/>), posed by <see cref="Turn"/> (it spins slowly by
    /// itself, and a hand turns it every way), framed whole by the cylinder it sweeps as it leans
    /// (<see cref="TurntableFraming"/>), so a coin and a sword both fill the view, and drawn every <see cref="Tick"/> into
    /// <see cref="Texture"/> on a transparent background for a UI to show. The subject is active only inside its own
    /// render, so no other camera ever sees it.
    /// </summary>
    public sealed class TurntablePreview : IDisposable
    {
        // A long lens flattens less than a wide one: things look as they do in the hand.
        const float FieldOfView = 30f;

        // The largest texture side, whatever the UI asks.
        const int MaxSide = 2048;

        readonly IAssetCatalog _assets;
        readonly PreviewStudio _studio;
        readonly Transform _turntable;
        GameObject _subject;
        RenderTexture _target;
        Vector3 _extents;

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

        /// <summary>How the subject is turned and zoomed: by itself, and by whoever drags it.</summary>
        public PreviewTurn Turn { get; } = new PreviewTurn();

        /// <summary>The picture, redrawn each <see cref="Tick"/>; null until <see cref="Resize"/> gives it a size.</summary>
        public RenderTexture Texture => _target;

        /// <summary>The asset key shown, or null for nothing.</summary>
        public string Shown { get; private set; }

        /// <summary>
        /// Shows <paramref name="assetKey"/>'s asset, upright and three-quarters on (<see cref="PreviewTurn.Reset"/>); null
        /// clears it. The same key again keeps how it was turned.
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
            // Its drawn centre sits on the turntable's pivot, so it turns in place.
            var bounds = PreviewStudio.BoundsOf(_subject);
            _subject.transform.localPosition -= _turntable.InverseTransformPoint(bounds.center);
            _extents = bounds.extents;
            _subject.SetActive(false);
            Turn.Reset();
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

        /// <summary>Lets <paramref name="deltaTime"/> seconds pass (it spins by itself unless held) and draws the picture.</summary>
        public void Tick(float deltaTime)
        {
            if (_subject == null || _target == null)
                return;

            Turn.Tick(deltaTime);
            _turntable.localRotation = Turn.Rotation;

            TurntableFraming.Swept(_extents, Turn.Tilt, out var reach, out var halfHeight);
            var distance = TurntableFraming.Distance(reach, halfHeight, FieldOfView, (float)_target.width / _target.height, Turn.Zoom);
            var pose = TurntableFraming.CameraPose(_turntable.position, distance);
            var camera = _studio.Camera;
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var radius = _extents.magnitude;
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
