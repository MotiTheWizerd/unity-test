using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// A decimal number input with an optional <see cref="unit"/>. Commits on Enter or focus loss (delayed), so a
    /// value that drives an undo step is not written on every keystroke.
    /// </summary>
    [UxmlElement]
    public partial class ForgeNumberField : FloatField
    {
        public const string Class = "forge-number-field";

        Label _unitLabel;
        string _unit = string.Empty;

        public ForgeNumberField()
        {
            FieldChrome.Apply(this);
            AddToClassList(Class);
            isDelayed = true;
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
