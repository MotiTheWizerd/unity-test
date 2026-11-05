using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// Tooltips for the running editor. UI Toolkit only shows an element's <c>tooltip</c> inside the Unity Editor, so
    /// one of these at a panel's root draws them: give any element a <c>tooltip</c> (in UXML or code) and it shows
    /// beside the element after a short rest, hidden on leave or press. The text may use rich text.
    /// </summary>
    public sealed class TooltipLayer
    {
        public const string Class = "forge-tooltip";

        const long DelayMilliseconds = 450;
        const float Gap = 8f;

        readonly VisualElement _root;
        readonly Label _label;
        IVisualElementScheduledItem _pending;
        VisualElement _current;
        bool _leaving;

        public TooltipLayer(VisualElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(Class);
            _label.style.position = Position.Absolute;
            _label.style.display = DisplayStyle.None;
            _root.Add(_label);

            _root.RegisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerOutEvent>(OnOut, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerDownEvent>(_ => Hide(), TrickleDown.TrickleDown);
        }

        // The pointer entered an element: the nearest ancestor-or-self with a tooltip owns it.
        void OnOver(PointerOverEvent evt)
        {
            _leaving = false;
            var owner = Owner(evt.target as VisualElement);
            if (owner == _current)
                return;
            Hide();
            _current = owner;
            if (owner != null)
                _pending = _root.schedule.Execute(() => Show(owner)).StartingIn(DelayMilliseconds);
        }

        // The pointer left an element. If no element takes it over (it went to the world), the tooltip goes.
        void OnOut(PointerOutEvent evt)
        {
            _leaving = true;
            _root.schedule.Execute(() =>
            {
                if (_leaving)
                    Hide();
            });
        }

        VisualElement Owner(VisualElement element)
        {
            for (var e = element; e != null && e != _root; e = e.parent)
            {
                if (!string.IsNullOrEmpty(e.tooltip))
                    return e;
            }

            return null;
        }

        void Show(VisualElement target)
        {
            if (target.panel == null || string.IsNullOrEmpty(target.tooltip))
                return;
            _label.text = target.tooltip;
            _label.style.display = DisplayStyle.Flex;
            // The label's size is only known after layout, so place it again once that has happened.
            _label.RegisterCallbackOnce<GeometryChangedEvent>(_ => Place(target));
            Place(target);
        }

        // Beside the target on the right, vertically centred; on its left when the right would leave the screen.
        void Place(VisualElement target)
        {
            var anchor = target.worldBound;
            var width = _label.resolvedStyle.width;
            var height = _label.resolvedStyle.height;
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
            _current = null;
            _label.style.display = DisplayStyle.None;
        }
    }
}
