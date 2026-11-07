using System;
using Forge.World.Assets;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// A <see cref="TurntablePreview"/> in the UI: the live picture fills the element at its own resolution, a drag turns
    /// the subject (held still meanwhile) and the wheel zooms. Whoever hosts it says what to show and ticks it while it is
    /// on screen; the inventory's item now, the character later.
    /// </summary>
    public sealed class TurntableView : VisualElement, IDisposable
    {
        public const string UssClass = "forge-turntable";

        // Degrees the subject turns per pixel dragged.
        const float DragDegrees = 0.5f;

        readonly TurntablePreview _preview;
        int _pointer = -1;
        float _lastX;

        public TurntableView(IAssetCatalog assets)
        {
            _preview = new TurntablePreview(assets);
            AddToClassList(UssClass);
            RegisterCallback<GeometryChangedEvent>(_ => FitTexture());
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(e => Release(e.pointerId));
            RegisterCallback<PointerCaptureOutEvent>(_ => Release(_pointer));
            RegisterCallback<WheelEvent>(OnWheel);
        }

        /// <summary>Shows <paramref name="assetKey"/>'s asset; null shows nothing.</summary>
        public void Show(string assetKey) => _preview.Show(assetKey);

        /// <summary>Turns and draws the picture for <paramref name="deltaTime"/> seconds; call it each frame it is on screen.</summary>
        public void Tick(float deltaTime)
        {
            _preview.Tick(deltaTime);
            MarkDirtyRepaint();
        }

        public void Dispose() => _preview.Dispose();

        // The picture as many pixels as the element covers on screen.
        void FitTexture()
        {
            var scale = panel != null ? panel.scaledPixelsPerPoint : 1f;
            var width = Mathf.RoundToInt(resolvedStyle.width * scale);
            var height = Mathf.RoundToInt(resolvedStyle.height * scale);
            if (width <= 0 || height <= 0)
                return;
            _preview.Resize(width, height);
            style.backgroundImage = Background.FromRenderTexture(_preview.Texture);
        }

        void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0 || _pointer >= 0)
                return;
            _pointer = e.pointerId;
            _lastX = e.position.x;
            _preview.Held = true;
            this.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (e.pointerId != _pointer)
                return;
            // Dragging right turns its front to the right, as a hand would.
            _preview.Turn((_lastX - e.position.x) * DragDegrees);
            _lastX = e.position.x;
        }

        void Release(int pointerId)
        {
            if (pointerId != _pointer || _pointer < 0)
                return;
            if (this.HasPointerCapture(_pointer))
                this.ReleasePointer(_pointer);
            _pointer = -1;
            _preview.Held = false;
        }

        void OnWheel(WheelEvent e)
        {
            if (e.delta.y == 0f)
                return;
            // Wheel forward (up) brings it nearer.
            _preview.Zoom(-Mathf.Sign(e.delta.y));
            e.StopPropagation();
        }
    }
}
