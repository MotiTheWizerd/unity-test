using System;
using UnityEngine;
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

        // SHORTCUT: the accent gold repeated from ForgeTokens.uss. Unity reads a text input's selection and caret
        // colours from the element, not from USS (its --unity-selection-color route never resolved here), so they
        // are set in code. Change --forge-accent and these two together.
        static readonly Color Selection = new Color(212f / 255f, 175f / 255f, 55f / 255f, 0.35f);
        static readonly Color Caret = new Color(212f / 255f, 175f / 255f, 55f / 255f, 1f);

        public static void Apply(VisualElement field) => field.AddToClassList(Class);

        /// <summary>Colours a text input's selected text and caret in the accent, instead of Unity's blue.</summary>
        public static void ApplyTextColours<T>(TextInputBaseField<T> box)
        {
            if (box == null) throw new ArgumentNullException(nameof(box));
            // The one that paints is the text element inside the input (not the field's label, which Q<TextElement>
            // on the field would find first). Unity resets its colours to blue every time its style resolves, after
            // any callback of that event, so they are set again one tick later.
            var text = box.Q(className: TextInputBaseField<T>.inputUssClassName)?.Q<TextElement>();
            if (text == null)
                return;
            Colour(text);
            text.RegisterCallback<CustomStyleResolvedEvent>(_ => text.schedule.Execute(() => Colour(text)));
            box.RegisterCallback<AttachToPanelEvent>(_ => text.schedule.Execute(() => Colour(text)));
            // Taking focus resets them too (the selection is only ever seen while focused).
            box.RegisterCallback<FocusInEvent>(_ => text.schedule.Execute(() => Colour(text)));
            text.RegisterCallback<FocusEvent>(_ => text.schedule.Execute(() => Colour(text)));
        }

        static void Colour(TextElement text)
        {
            text.selection.selectionColor = Selection;
            text.selection.cursorColor = Caret;
        }

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
