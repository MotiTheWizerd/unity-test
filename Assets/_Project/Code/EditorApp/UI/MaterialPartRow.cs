using System;
using Forge.EditorApp.UI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// One material slot of the selection (<see cref="MaterialSlotList"/>): a small ball of what it wears, the part's name
    /// and the material's name. Clicking it chooses the slot; the chosen one is lit.
    /// </summary>
    public sealed class MaterialPartRow : Button
    {
        public const string Class = "forge-slot-row";
        const string BallClass = "forge-slot-row__ball";
        const string NameClass = "forge-slot-row__name";
        const string WornClass = "forge-slot-row__worn";
        const string PlaceholderIcon = "palette";

        readonly VisualElement _ball;
        readonly VisualElement _placeholder;
        readonly Label _worn;

        public MaterialPartRow(string slot, string label, Action<string> pick)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            if (pick == null)
                throw new ArgumentNullException(nameof(pick));
            clicked += () => pick(Slot);
            AddToClassList(Class);

            _ball = new VisualElement { pickingMode = PickingMode.Ignore };
            _ball.AddToClassList(BallClass);
            _placeholder = Icons.Make(PlaceholderIcon);
            _ball.Add(_placeholder);
            Add(_ball);

            var name = new Label(label) { pickingMode = PickingMode.Ignore };
            name.AddToClassList(NameClass);
            Add(name);

            _worn = new Label { pickingMode = PickingMode.Ignore };
            _worn.AddToClassList(WornClass);
            Add(_worn);
        }

        public string Slot { get; }

        /// <summary>Shows what the slot wears: its name, and its ball (null: the placeholder icon).</summary>
        public void Show(string worn, Texture2D ball)
        {
            PanelElements.SetText(_worn, worn);
            _ball.style.backgroundImage = ball != null ? new StyleBackground(ball) : StyleKeyword.Null;
            PanelElements.SetVisible(_placeholder, ball == null);
        }
    }
}
