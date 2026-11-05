using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// A slider with its number box shown by default (as wide as its <see cref="digits"/>, four unless told
    /// otherwise) and an optional <see cref="unit"/> after it.
    /// </summary>
    [UxmlElement]
    public partial class ForgeSlider : Slider
    {
        public const string Class = "forge-slider";

        readonly TextField _box;
        Label _unitLabel;
        string _unit = string.Empty;
        int _digits = FieldChrome.DefaultDigits;

        public ForgeSlider()
        {
            FieldChrome.Apply(this);
            AddToClassList(Class);
            showInputField = true;
            // The number box takes the number fields' look (ForgeFields.uss), so every number in the UI matches.
            _box = this.Q<TextField>(className: textFieldClassName);
            _box.AddToClassList(ForgeNumberField.Class);
            FieldChrome.SetDigits(_box, _digits);
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

        /// <summary>Characters the number box accepts and shows; a sign or a decimal point counts as one.</summary>
        [UxmlAttribute]
        public int digits
        {
            get => _digits;
            set
            {
                _digits = value;
                FieldChrome.SetDigits(_box, value);
            }
        }
    }
}
