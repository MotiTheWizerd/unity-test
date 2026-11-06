using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// One page of a <see cref="ForgeSideDock"/>: its <see cref="key"/> names it in code, its tab shows its
    /// <see cref="icon"/> (or its <see cref="title"/>). UXML children are the page's content. Styled by ForgeSideDock.uss.
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

        /// <summary>The page's name for people: its tab's tooltip, or the tab's text when it has no <see cref="icon"/>.</summary>
        [UxmlAttribute]
        public string title { get; set; } = string.Empty;

        /// <summary>The icon of the page's tab (an icon name, as <see cref="ForgeButton.icon"/> takes).</summary>
        [UxmlAttribute]
        public string icon { get; set; } = string.Empty;
    }
}
