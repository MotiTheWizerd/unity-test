using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>A multi-line text box (messages, descriptions): wraps, grows with its text within its max height.</summary>
    [UxmlElement]
    public partial class ForgeTextArea : TextField
    {
        public const string Class = "forge-text-area";

        public ForgeTextArea()
        {
            FieldChrome.Apply(this);
            AddToClassList(Class);
            multiline = true;
        }
    }
}
