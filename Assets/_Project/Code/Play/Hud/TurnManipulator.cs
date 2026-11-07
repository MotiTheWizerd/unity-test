using System;
using Forge.World.Assets;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// Turning a 3D thing in the UI by hand, as Skyrim's inventory does, for any element that shows one: a left drag
    /// turns it every way (across spins it, up and down tilt it; it holds still meanwhile), the wheel zooms, a double
    /// click puts it back as it started. It only feeds a <see cref="PreviewTurn"/>; whatever draws the thing reads it.
    /// Add it to an element with <c>AddManipulator</c>.
    /// </summary>
    public sealed class TurnManipulator : PointerManipulator
    {
        /// <summary>Degrees the thing turns per pixel dragged.</summary>
        public const float DegreesPerPixel = 0.5f;

        readonly PreviewTurn _turn;
        int _pointer = -1;
        Vector2 _last;

        public TurnManipulator(PreviewTurn turn)
        {
            _turn = turn ?? throw new ArgumentNullException(nameof(turn));
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            target.RegisterCallback<WheelEvent>(OnWheel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            target.UnregisterCallback<WheelEvent>(OnWheel);
            Release();
        }

        void OnPointerDown(PointerDownEvent e)
        {
            if (_pointer >= 0 || !CanStartManipulation(e))
                return;
            e.StopPropagation();
            if (e.clickCount == 2)
            {
                _turn.Reset();
                return;
            }

            _pointer = e.pointerId;
            _last = e.position;
            _turn.Held = true;
            target.CapturePointer(e.pointerId);
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (e.pointerId != _pointer)
                return;
            var moved = (Vector2)e.position - _last;
            _last = e.position;
            // Panel y grows downward; dragging up tilts the near side up.
            _turn.Drag(moved.x * DegreesPerPixel, -moved.y * DegreesPerPixel);
            e.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent e)
        {
            if (e.pointerId != _pointer || !CanStopManipulation(e))
                return;
            Release();
            e.StopPropagation();
        }

        void OnCaptureOut(PointerCaptureOutEvent e) => Release();

        void OnWheel(WheelEvent e)
        {
            if (e.delta.y == 0f)
                return;
            // Wheel forward (up) brings it nearer.
            _turn.ZoomBy(-Mathf.Sign(e.delta.y));
            e.StopPropagation();
        }

        void Release()
        {
            if (_pointer < 0)
                return;
            var pointer = _pointer;
            _pointer = -1;
            _turn.Held = false;
            if (target != null && target.HasPointerCapture(pointer))
                target.ReleasePointer(pointer);
        }
    }
}
