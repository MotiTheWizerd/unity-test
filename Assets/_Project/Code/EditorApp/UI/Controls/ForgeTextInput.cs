using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>A single-line text input with Forge's field chrome and an optional <see cref="unit"/>.</summary>
    [UxmlElement]
    public partial class ForgeTextInput : TextField
    {
        public const string Class = "forge-text-input";

        Label _unitLabel;
        string _unit = string.Empty;

        public ForgeTextInput()
        {
            FieldChrome.Apply(this);
            AddToClassList(Class);
        }

        [UxmlAttribute]
        public string unit
        {
            get => _unit;
            set
            {
                _unit = value ?? string.Empty;
                _unitLabel = FieldChrome.SetUnit(this, _unitLabel, _unit);
            }
        }
    }
}
