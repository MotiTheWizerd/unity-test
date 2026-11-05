using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// A card of dark glass: Forge's container for anything that floats over the world. An optional
    /// <see cref="title"/> adds a title bar; UXML children go into the body. A panel that needs more in its title bar
    /// (a status label, a button) composes the bar itself with the same classes (see ForgePanel.uss).
    /// </summary>
    [UxmlElement]
    public partial class ForgePanel : VisualElement
    {
        public const string Class = "forge-panel";
        public const string TitleBarClass = "forge-panel__title-bar";
        public const string TitleClass = "forge-panel__title";
        public const string BodyClass = "forge-panel__body";

        readonly VisualElement _body;
        VisualElement _titleBar;
        Label _title;
        string _titleText = string.Empty;

        public ForgePanel()
        {
            AddToClassList(Class);
            _body = new VisualElement { name = "body" };
            _body.AddToClassList(BodyClass);
            hierarchy.Add(_body);
        }

        public override VisualElement contentContainer => _body;

        /// <summary>The title bar's text; empty hides the bar.</summary>
        [UxmlAttribute]
        public string title
        {
            get => _titleText;
            set
            {
                _titleText = value ?? string.Empty;
                if (_titleText.Length == 0)
                {
                    _titleBar?.RemoveFromHierarchy();
                    _titleBar = null;
                    _title = null;
                    return;
                }

                if (_titleBar == null)
                {
                    _titleBar = new VisualElement();
                    _titleBar.AddToClassList(TitleBarClass);
                    _title = new Label();
                    _title.AddToClassList(TitleClass);
                    _titleBar.Add(_title);
                    hierarchy.Insert(0, _titleBar);
                }

                _title.text = _titleText;
            }
        }
    }
}
