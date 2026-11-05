using System;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// What every Forge field shares, whatever Unity control it wraps: the field class, and a unit label after the
    /// input ("m", "°"). Fields are thin subclasses of Unity's controls, so this is a helper, not a base class.
    /// </summary>
    static class FieldChrome
    {
        public const string Class = "forge-field";
        public const string UnitClass = "forge-field__unit";

        public static void Apply(VisualElement field) => field.AddToClassList(Class);

        /// <summary>Shows <paramref name="unit"/> after the input, reusing <paramref name="current"/>; empty removes it.</summary>
        public static Label SetUnit(VisualElement field, Label current, string unit)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (string.IsNullOrEmpty(unit))
            {
                current?.RemoveFromHierarchy();
                return null;
            }

            if (current == null)
            {
                current = new Label { pickingMode = PickingMode.Ignore };
                current.AddToClassList(UnitClass);
                field.hierarchy.Add(current);
            }

            current.text = unit;
            return current;
        }
    }
}
