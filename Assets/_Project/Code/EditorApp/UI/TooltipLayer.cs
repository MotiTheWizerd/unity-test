using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// Tooltips for the running editor. UI Toolkit only shows an element's <c>tooltip</c> inside the Unity Editor, so
    /// the panel draws its own: one label at the root, shown beside the hovered element after a short rest and hidden
    /// on leave or press. Any element can have one (<see cref="Attach"/>); the text may use rich text.
    /// </summary>
    public sealed class TooltipLayer
    {
        public const string Class = "forge-tooltip";

        const long DelayMilliseconds = 450;
        const float Gap = 8f;

        readonly VisualElement _root;
        readonly Label _label;
        IVisualElementScheduledItem _pending;

        public TooltipLayer(VisualElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(Class);
            _label.style.position = Position.Absolute;
            PanelElements.SetVisible(_label, false);
            _root.Add(_label);
        }

        /// <summary>Shows <paramref name="text"/> beside <paramref name="target"/> while the pointer rests on it.</summary>
        public void Attach(VisualElement target, string text)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("A tooltip needs text.", nameof(text));
            target.RegisterCallback<PointerEnterEvent>(_ => Schedule(target, text));
            target.RegisterCallback<PointerLeaveEvent>(_ => Hide());
            target.RegisterCallback<PointerDownEvent>(_ => Hide(), TrickleDown.TrickleDown);
        }

        void Schedule(VisualElement target, string text)
        {
            _pending?.Pause();
            _pending = _root.schedule.Execute(() => Show(target, text)).StartingIn(DelayMilliseconds);
        }

        void Show(VisualElement target, string text)
        {
            if (target.panel == null)
                return;
            _label.text = text;
            PanelElements.SetVisible(_label, true);
            // The label's size is only known after layout, so place it once that has happened.
            _label.RegisterCallbackOnce<GeometryChangedEvent>(_ => Place(target));
            Place(target);
        }

        // Beside the target on the right, vertically centred; on its left when the right would leave the screen.
        void Place(VisualElement target)
        {
            var anchor = target.worldBound;
            var size = _label.resolvedStyle;
            var width = size.width;
            var height = size.height;
            var screen = _root.worldBound;

            var left = anchor.xMax + Gap;
            if (left + width > screen.xMax)
                left = anchor.xMin - Gap - width;
            var top = Mathf.Clamp(anchor.center.y - height / 2f, screen.yMin, Mathf.Max(screen.yMin, screen.yMax - height));

            _label.style.left = left - screen.xMin;
            _label.style.top = top - screen.yMin;
        }

        void Hide()
        {
            _pending?.Pause();
            _pending = null;
            PanelElements.SetVisible(_label, false);
        }
    }
}
