using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// A decimal number input: a bare box as wide as its <see cref="digits"/> (four unless told otherwise), with an
    /// optional <see cref="unit"/>. Commits on Enter or focus loss (delayed), so a value that drives an undo step is
    /// not written on every keystroke.
    /// </summary>
    [UxmlElement]
    public partial class ForgeNumberField : FloatField
    {
        public const string Class = "forge-number-field";

        Label _unitLabel;
        string _unit = string.Empty;
        int _digits = FieldChrome.DefaultDigits;

        public ForgeNumberField()
        {
            FieldChrome.Apply(this);
            FieldChrome.ApplyTextColours(this);
            AddToClassList(Class);
            isDelayed = true;
            FieldChrome.SetDigits(this, _digits);
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

        /// <summary>Characters the box accepts and shows; a sign or a decimal point counts as one.</summary>
        [UxmlAttribute]
        public int digits
        {
            get => _digits;
            set
            {
                _digits = value;
                FieldChrome.SetDigits(this, value);
            }
        }
    }
}
