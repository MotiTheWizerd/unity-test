using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// A thin divider line. Horizontal between stacked content; <see cref="vertical"/> between the items of a row
    /// (a toolbar). Styled by ForgeSeparator.uss with the divider token.
    /// </summary>
    [UxmlElement]
    public partial class ForgeSeparator : VisualElement
    {
        public const string Class = "forge-separator";
        public const string VerticalClass = "forge-separator--vertical";

        bool _vertical;

        public ForgeSeparator()
        {
            AddToClassList(Class);
            pickingMode = PickingMode.Ignore;
        }

        /// <summary>A vertical line for rows; the default is a horizontal line for stacked content.</summary>
        [UxmlAttribute]
        public bool vertical
        {
            get => _vertical;
            set
            {
                _vertical = value;
                EnableInClassList(VerticalClass, value);
            }
        }
    }
}
