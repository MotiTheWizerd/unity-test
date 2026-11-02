using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>A row of buttons, one per value of an enum, with the current value highlighted (a segmented control).</summary>
    public sealed class ChoiceRow<T> where T : struct, Enum
    {
        readonly Dictionary<T, Button> _buttons = new Dictionary<T, Button>();

        public ChoiceRow(VisualElement container, Func<T, string> label, Action<T> select)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            container.Clear();
            foreach (T value in Enum.GetValues(typeof(T)))
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
    }
}
