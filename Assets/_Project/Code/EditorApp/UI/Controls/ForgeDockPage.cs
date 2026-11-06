using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// One page of a <see cref="ForgeSideDock"/>: its <see cref="key"/> names it in code, its <see cref="title"/> is its
    /// tab. UXML children are the page's content. Styled by ForgeSideDock.uss.
    /// </summary>
    [UxmlElement]
    public partial class ForgeDockPage : VisualElement
    {
        public const string Class = "forge-dock-page";

        public ForgeDockPage()
        {
            AddToClassList(Class);
        }

        /// <summary>The page's name in code (<see cref="ForgeSideDock.Show"/>); unique within its dock.</summary>
        [UxmlAttribute]
        public string key { get; set; } = string.Empty;

        /// <summary>The text of the page's tab.</summary>
        [UxmlAttribute]
        public string title { get; set; } = string.Empty;
    }
}
