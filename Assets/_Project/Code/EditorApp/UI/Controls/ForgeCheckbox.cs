using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>An on/off box with its text beside it. The box is the themed Unity toggle; this adds the field chrome.</summary>
    [UxmlElement]
    public partial class ForgeCheckbox : Toggle
    {
        public const string Class = "forge-checkbox";

        public ForgeCheckbox()
        {
            FieldChrome.Apply(this);
            AddToClassList(Class);
        }
    }
}
