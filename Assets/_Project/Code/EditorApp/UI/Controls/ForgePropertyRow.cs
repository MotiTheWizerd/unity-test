using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// One property in an inspector: a name in a fixed label column and its value beside it (any elements added to the
    /// row, or text through <see cref="SetText"/>). Styled by ForgePropertyRow.uss.
    /// </summary>
    [UxmlElement]
    public partial class ForgePropertyRow : VisualElement
    {
        public const string Class = "forge-property-row";
        const string LabelClass = "forge-property-row__label";
        const string ValueClass = "forge-property-row__value";
        const string TextClass = "forge-property-row__text";

        readonly Label _label;
        readonly VisualElement _value;
        Label _text;

        public ForgePropertyRow() : this(string.Empty)
        {
        }

        public ForgePropertyRow(string label)
        {
            AddToClassList(Class);
            _label = new Label(label);
            _label.AddToClassList(LabelClass);
            hierarchy.Add(_label);
            _value = new VisualElement();
            _value.AddToClassList(ValueClass);
            hierarchy.Add(_value);
        }

        public override VisualElement contentContainer => _value;

        [UxmlAttribute]
        public string label
        {
            get => _label.text;
            set => _label.text = value;
        }

        /// <summary>Shows <paramref name="text"/> as the value (selectable, so an id can be copied).</summary>
        public void SetText(string text)
        {
            if (_text == null)
            {
                _text = new Label { selection = { isSelectable = true } };
                _text.AddToClassList(TextClass);
                Add(_text);
            }

            PanelElements.SetText(_text, text ?? string.Empty);
        }
    }
}
