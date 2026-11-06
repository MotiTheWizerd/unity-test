using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// Makes an <see cref="AssetTile"/> draggable into the world: once the pressed pointer moves past a few pixels, a
    /// small copy of the tile's picture follows it (on the panel's root, ignoring picking); letting go anywhere off the
    /// panel's elements is a drop. A tile still clicks as before when it isn't dragged. What a drop does is the caller's:
    /// the controls know nothing of the world.
    /// </summary>
    public sealed class TileDrag : PointerManipulator
    {
        public const string GhostClass = "forge-tile-drag";

        /// <summary>Pixels the pointer moves before a press becomes a drag, so a click never drags.</summary>
        const float DeadZone = 8f;

        readonly Action _begin;
        readonly Action _drop;
        readonly Action _end;
        Vector2 _start;
        int _pointer = -1;
        VisualElement _ghost;

        /// <param name="begin">A drag started.</param>
        /// <param name="drop">The drag ended off the panel's elements (over the world).</param>
        /// <param name="end">The drag ended, dropped or not; called after <paramref name="drop"/>.</param>
        public TileDrag(Action begin, Action drop, Action end)
        {
            _begin = begin ?? throw new ArgumentNullException(nameof(begin));
            _drop = drop ?? throw new ArgumentNullException(nameof(drop));
            _end = end ?? throw new ArgumentNullException(nameof(end));
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        bool Dragging => _ghost != null;

        protected override void RegisterCallbacksOnTarget()
        {
            // Trickle-down, so a finished drag is seen before the tile's own click handling.
            target.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        void OnDown(PointerDownEvent e)
        {
            if (!CanStartManipulation(e))
                return;
            _pointer = e.pointerId;
            _start = e.position;
        }

        void OnMove(PointerMoveEvent e)
        {
            if (e.pointerId != _pointer)
                return;
            if (!Dragging)
            {
                if (Vector2.Distance(e.position, _start) < DeadZone)
                    return;
                Begin();
            }

            Follow(e.position);
            e.StopPropagation();
        }

        void OnUp(PointerUpEvent e)
        {
            if (e.pointerId != _pointer)
                return;
            _pointer = -1;
            if (!Dragging)
                return;

            // Off every panel element means over the world.
            var overPanel = target.panel?.Pick(e.position) != null;
            if (target.HasPointerCapture(e.pointerId))
                target.ReleasePointer(e.pointerId);
            // The tile mustn't also take this release as a click.
            e.StopImmediatePropagation();
            Finish(drop: !overPanel);
        }

        void OnCaptureOut(PointerCaptureOutEvent e)
        {
            // Lost the pointer some other way (focus left the window): the drag ends without a drop.
            if (Dragging)
                Finish(drop: false);
            _pointer = -1;
        }

        void Begin()
        {
            if (!target.HasPointerCapture(_pointer))
                target.CapturePointer(_pointer);

            _ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            _ghost.AddToClassList(GhostClass);
            if (target is AssetTile tile && tile.Picture != null)
                _ghost.style.backgroundImage = new StyleBackground(tile.Picture);
            target.panel.visualTree.Add(_ghost);
            _begin();
        }

        void Follow(Vector2 position)
        {
            // Before its first layout the ghost has no size yet; its sheet makes it 56 px.
            var width = float.IsNaN(_ghost.resolvedStyle.width) ? 56f : _ghost.resolvedStyle.width;
            var height = float.IsNaN(_ghost.resolvedStyle.height) ? 56f : _ghost.resolvedStyle.height;
            _ghost.style.left = position.x - width * 0.5f;
            _ghost.style.top = position.y - height * 0.5f;
        }

        void Finish(bool drop)
        {
            _ghost.RemoveFromHierarchy();
            _ghost = null;
            try
            {
                if (drop)
                    _drop();
            }
            finally
            {
                _end();
            }
        }
    }
}
