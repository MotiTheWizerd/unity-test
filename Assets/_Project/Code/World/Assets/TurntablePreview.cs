using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// A thing shown live in a <see cref="PreviewStudio"/>, as Skyrim shows the item chosen in its inventory and the
    /// character carrying it: built by whoever shows it (<see cref="Show(object, Func{Transform, GameObject})"/>; an item
    /// from the world's assets, a character from its prefab), posed by <see cref="Turn"/> (an item spins slowly by itself
    /// and a hand turns it every way; a character only spins), framed whole by the cylinder it sweeps as it leans
    /// (<see cref="TurntableFraming"/>), so a coin, a sword and a man all fill the view, and drawn every
    /// <see cref="Tick"/> into <see cref="Texture"/> on a transparent background for a UI to show. The subject is only a
    /// picture: its colliders go, it draws only inside its own render, so no other camera ever sees it, and its
    /// animators (a character's idle) step with each tick rather than with the world, so it breathes while the world waits.
    /// </summary>
    public sealed class TurntablePreview : IDisposable
    {
        // A long lens flattens less than a wide one: things look as they do in the hand.
        const float FieldOfView = 30f;

        // The largest texture side, whatever the UI asks.
        const int MaxSide = 2048;

        readonly PreviewStudio _studio;
        readonly Transform _turntable;
        readonly List<Renderer> _renderers = new List<Renderer>();
        GameObject _subject;
        Animator[] _animators = Array.Empty<Animator>();
        RenderTexture _target;
        Vector3 _extents;

        /// <param name="turn">How the subject turns (<see cref="PreviewTurn.Character"/> for a character); null for an item's.</param>
        public TurntablePreview(PreviewTurn turn = null)
        {
            Turn = turn ?? new PreviewTurn();
            _studio = new PreviewStudio("Turntable", null);
            var camera = _studio.Camera;
            camera.orthographic = false;
            camera.fieldOfView = FieldOfView;
            camera.allowMSAA = true;
            _turntable = new GameObject("Turntable").transform;
            _turntable.SetParent(_studio.Stage, false);
        }

        /// <summary>How the subject is turned and zoomed: by itself, and by whoever drags it.</summary>
        public PreviewTurn Turn { get; }

        /// <summary>The picture, redrawn each <see cref="Tick"/>; null until <see cref="Resize"/> gives it a size.</summary>
        public RenderTexture Texture => _target;

        /// <summary>Where the picture is seen from (its camera; the scene's), as of the last <see cref="Tick"/>.</summary>
        public Vector3 Viewer => _studio.Camera.transform.position;

        /// <summary>What is shown (an asset key, a prefab), or null for nothing.</summary>
        public object Shown { get; private set; }

        /// <summary>Shows <paramref name="assetKey"/>'s asset from <paramref name="assets"/>; null clears it.</summary>
        public void Show(IAssetCatalog assets, string assetKey)
        {
            if (assets == null)
                throw new ArgumentNullException(nameof(assets));
            Show(string.IsNullOrEmpty(assetKey) ? null : assetKey, parent => assets.Instantiate(assetKey, parent));
        }

        /// <summary>
        /// Shows <paramref name="subject"/>, made by <paramref name="build"/> under the parent it is given, upright and at
        /// its start (<see cref="PreviewTurn.Reset"/>), or as it was turned when <paramref name="keepTurn"/> (the same
        /// character, dressed anew); null clears it. The same subject again changes nothing.
        /// </summary>
        public void Show(object subject, Func<Transform, GameObject> build, bool keepTurn = false)
        {
            if (Equals(subject, Shown))
                return;

            var wasShown = _subject != null;
            Clear();
            if (subject == null)
                return;
            if (build == null)
                throw new ArgumentNullException(nameof(build));

            _turntable.localRotation = Quaternion.identity;
            _subject = build(_turntable);
            if (_subject == null)
                return;
            Shown = subject;
            MakePicture(_subject);
            _studio.PutOnLayer(_subject);
            // Its drawn centre sits on the turntable's pivot, so it turns in place.
            var bounds = PreviewStudio.BoundsOf(_subject);
            _subject.transform.localPosition -= _turntable.InverseTransformPoint(bounds.center);
            _extents = bounds.extents;
            if (!(keepTurn && wasShown))
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
            Wipe();
        }

        /// <summary>
        /// Lets <paramref name="deltaTime"/> seconds pass (it spins by itself unless held, its animations play) and draws
        /// the picture.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_subject == null || _target == null)
                return;

            Turn.Tick(deltaTime);
            _turntable.localRotation = Turn.Rotation;
            foreach (var animator in _animators)
                animator.Update(deltaTime);

            TurntableFraming.Swept(_extents, Turn.Tilt, out var reach, out var halfHeight);
            var distance = TurntableFraming.Distance(reach, halfHeight, FieldOfView, (float)_target.width / _target.height, Turn.Zoom, Turn.Pitch);
            var aim = TurntableFraming.Aim(_turntable.position, reach, halfHeight, Turn.Pan);
            var pose = TurntableFraming.CameraPose(aim, distance, Turn.Pitch);
            var camera = _studio.Camera;
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var radius = _extents.magnitude;
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            camera.farClipPlane = distance + radius * 2f;

            Draw(true);
            try
            {
                _studio.Render(_target, true);
            }
            finally
            {
                Draw(false);
            }
        }

        public void Dispose()
        {
            Clear();
            _studio.Dispose();
            ReleaseTarget();
        }

        // Nothing touches it (no colliders), it draws only when asked, and its animators wait for the ticks.
        void MakePicture(GameObject subject)
        {
            foreach (var collider in subject.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                PreviewStudio.Discard(collider);
            }

            _animators = subject.GetComponentsInChildren<Animator>(true);
            foreach (var animator in _animators)
            {
                animator.enabled = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // Its first pose (a character's idle, not its T-pose) before it is measured.
                animator.Update(0f);
            }

            Draw(false);
        }

        // What it draws is read as it is drawn: a subject may change what it wears while it is shown (another head put on
        // in the face editor), what it took off gone and what it put on drawn only here, as the rest of it is.
        void Draw(bool drawn)
        {
            if (_subject == null)
                return;
            _subject.GetComponentsInChildren(true, _renderers);
            foreach (var renderer in _renderers)
                renderer.forceRenderingOff = !drawn;
        }

        void Clear()
        {
            if (_subject != null)
                PreviewStudio.Discard(_subject);
            _subject = null;
            _renderers.Clear();
            _animators = Array.Empty<Animator>();
            Shown = null;
            Wipe();
        }

        // Nothing drawn: the picture is only drawn while there is a subject, so without this the last one would stay.
        void Wipe()
        {
            if (_target == null)
                return;
            var active = RenderTexture.active;
            RenderTexture.active = _target;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = active;
        }

        void ReleaseTarget()
        {
            if (_target == null)
                return;
            _target.Release();
            PreviewStudio.Discard(_target);
            _target = null;
        }
    }
}
