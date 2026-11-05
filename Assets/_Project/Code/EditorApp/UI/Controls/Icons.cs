using System;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// Forge's icon set: Lucide SVGs in UI/Icons, each mapped by name in Theme/ForgeIcons.uss. An icon element
    /// carries .forge-icon and .forge-icon--{name}; its colour is the background tint, set by the surrounding style.
    /// </summary>
    public static class Icons
    {
        public const string Class = "forge-icon";

        public static VisualElement Make(string name)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(Class);
            Set(icon, null, name);
            return icon;
        }

        /// <summary>Swaps the icon an element shows; null or empty clears it.</summary>
        public static void Set(VisualElement icon, string previous, string next)
        {
            if (icon == null) throw new ArgumentNullException(nameof(icon));
            if (!string.IsNullOrEmpty(previous))
                icon.RemoveFromClassList(ClassFor(previous));
            if (!string.IsNullOrEmpty(next))
                icon.AddToClassList(ClassFor(next));
        }

        public static string ClassFor(string name) => $"{Class}--{name}";
    }
}
