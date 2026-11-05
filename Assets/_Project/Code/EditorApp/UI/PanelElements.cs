using System;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>Small helpers shared by the panel sections.</summary>
    public static class PanelElements
    {
        /// <summary>Marks the selected tool, mode or palette entry.</summary>
        public const string ActiveClass = "is-active";

        public const string HiddenClass = "forge-hidden";

        public const string IconClass = "forge-icon";

        /// <summary>An icon from the set in UI/Icons, by name (styled by Theme/ForgeIcons.uss), e.g. "move".</summary>
        public static VisualElement Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("An icon needs a name.", nameof(name));
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(IconClass);
            icon.AddToClassList($"{IconClass}--{name}");
            return icon;
        }

        /// <summary>
        /// The element named <paramref name="name"/>. Throws if it is missing, so a
        /// mismatch between UXML and code fails loudly at startup instead of silently.
        /// </summary>
        public static T Required<T>(VisualElement root, string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException($"Editor panel UXML has no {typeof(T).Name} named '{name}'.");

        public static void SetVisible(VisualElement element, bool visible) =>
            element.EnableInClassList(HiddenClass, !visible);

        public static void SetActive(VisualElement element, bool active) =>
            element.EnableInClassList(ActiveClass, active);

        /// <summary>Sets text only when it changed, so per-frame refreshes don't dirty the layout.</summary>
        public static void SetText(TextElement element, string text)
        {
            if (element.text != text)
                element.text = text;
        }
    }
}
