using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>A pick-one list. The closed field and the open menu are the themed Unity dropdown; this adds the field chrome.</summary>
    [UxmlElement]
    public partial class ForgeDropdown : DropdownField
    {
        public const string Class = "forge-dropdown";

        public ForgeDropdown()
        {
            FieldChrome.Apply(this);
            AddToClassList(Class);
        }
    }
}
