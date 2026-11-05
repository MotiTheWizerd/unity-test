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

        /// <summary>Characters a number box accepts and shows unless told otherwise.</summary>
        public const int DefaultDigits = 4;

        const float DigitWidth = 9f;
        const float DigitPadding = 8f;

        public static void Apply(VisualElement field) => field.AddToClassList(Class);

        /// <summary>
        /// Sizes a number box to <paramref name="digits"/> characters and refuses more (a sign or a decimal point
        /// counts as one). The width follows the count so the box is always just as wide as its longest value.
        /// </summary>
        public static void SetDigits<T>(TextInputBaseField<T> box, int digits)
        {
            if (box == null) throw new ArgumentNullException(nameof(box));
            if (digits < 1) throw new ArgumentOutOfRangeException(nameof(digits), digits, "A number box shows at least one digit.");
            box.maxLength = digits;
            // The text input ignores an inline width (Unity keeps its own), but honours min and max.
            var input = box.Q(className: TextInputBaseField<T>.inputUssClassName);
            if (input == null)
                return;
            var width = digits * DigitWidth + DigitPadding;
            input.style.minWidth = width;
            input.style.maxWidth = width;
        }

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
