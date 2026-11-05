using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>A slider with its number box shown by default and an optional <see cref="unit"/> after it.</summary>
    [UxmlElement]
    public partial class ForgeSlider : Slider
    {
        public const string Class = "forge-slider";

        Label _unitLabel;
        string _unit = string.Empty;

        public ForgeSlider()
        {
            FieldChrome.Apply(this);
            AddToClassList(Class);
            showInputField = true;
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
