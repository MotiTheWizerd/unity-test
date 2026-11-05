using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// A row of buttons, one per value, with the current value highlighted (a segmented control). Give it an enum
    /// and it offers every value, or any list of values (sizes, spacings) with a label each.
    /// </summary>
    public sealed class ChoiceRow<T> where T : struct
    {
        readonly Dictionary<T, Button> _buttons = new Dictionary<T, Button>();

        /// <summary>One button per value of the enum <typeparamref name="T"/>.</summary>
        public ChoiceRow(VisualElement container, Func<T, string> label, Action<T> select)
            : this(container, EnumValues(), label, select)
        {
        }

        /// <summary>One button per value in <paramref name="values"/>, in that order.</summary>
        public ChoiceRow(VisualElement container, IEnumerable<T> values, Func<T, string> label, Action<T> select)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            container.Clear();
            foreach (var value in values)
            {
                var button = new Button(() => select(value)) { text = label(value) };
                container.Add(button);
                _buttons.Add(value, button);
            }
        }

        /// <summary>Highlights <paramref name="active"/>; null highlights nothing.</summary>
        public void Refresh(T? active)
        {
            foreach (var pair in _buttons)
                PanelElements.SetActive(pair.Value, active.HasValue && EqualityComparer<T>.Default.Equals(pair.Key, active.Value));
        }

        static IEnumerable<T> EnumValues()
        {
            if (!typeof(T).IsEnum)
                throw new InvalidOperationException($"{typeof(T).Name} is not an enum; give the row its values.");
            return Enum.GetValues(typeof(T)).Cast<T>();
        }
    }
}
