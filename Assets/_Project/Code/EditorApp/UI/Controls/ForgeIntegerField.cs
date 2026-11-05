using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>A whole-number input with an optional <see cref="unit"/>; commits on Enter or focus loss.</summary>
    [UxmlElement]
    public partial class ForgeIntegerField : IntegerField
    {
        public const string Class = "forge-number-field";

        Label _unitLabel;
        string _unit = string.Empty;

        public ForgeIntegerField()
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
