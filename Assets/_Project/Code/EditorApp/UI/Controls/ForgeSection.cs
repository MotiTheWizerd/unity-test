using System;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// A titled block inside a panel: a small-caps header over its content, with a rule underneath. A
    /// <see cref="collapsible"/> section folds to its header on a click, showing a chevron. UXML children go into
    /// the content. Replaces the hand-made section rows and Unity's Foldout.
    /// </summary>
    [UxmlElement]
    public partial class ForgeSection : VisualElement
    {
        public const string Class = "forge-section";
        public const string HeaderClass = "forge-section__header";
        public const string TitleClass = "forge-section__title";
        public const string ChevronClass = "forge-section__chevron";
        public const string ContentClass = "forge-section__content";
        public const string CollapsibleClass = "is-collapsible";
        public const string CollapsedClass = "is-collapsed";
        const string ChevronIcon = "chevron-down";

        readonly VisualElement _header;
        readonly Label _title;
        readonly VisualElement _content;
        VisualElement _chevron;
        bool _collapsible;
        bool _collapsed;

        public ForgeSection()
        {
            AddToClassList(Class);
            _header = new VisualElement();
            _header.AddToClassList(HeaderClass);
            _title = new Label();
            _title.AddToClassList(TitleClass);
            _header.Add(_title);
            _header.RegisterCallback<ClickEvent>(_ =>
            {
                if (_collapsible)
                    collapsed = !_collapsed;
            });
            hierarchy.Add(_header);

            _content = new VisualElement { name = "content" };
            _content.AddToClassList(ContentClass);
            hierarchy.Add(_content);
        }

        public override VisualElement contentContainer => _content;

        [UxmlAttribute]
        public string title
        {
            get => _title.text;
            set => _title.text = value ?? string.Empty;
        }

        /// <summary>Whether a click on the header folds the section.</summary>
        [UxmlAttribute]
        public bool collapsible
        {
            get => _collapsible;
            set
            {
                _collapsible = value;
                EnableInClassList(CollapsibleClass, value);
                if (value && _chevron == null)
                {
                    _chevron = Icons.Make(ChevronIcon);
                    _chevron.AddToClassList(ChevronClass);
                    _header.Add(_chevron);
                }
                else if (!value && _chevron != null)
                {
                    _chevron.RemoveFromHierarchy();
                    _chevron = null;
                    collapsed = false;
                }
            }
        }

        /// <summary>Folded to the header. Only meaningful while <see cref="collapsible"/>.</summary>
        [UxmlAttribute]
        public bool collapsed
        {
            get => _collapsed;
            set
            {
                if (_collapsed == value)
                    return;
                _collapsed = value;
                EnableInClassList(CollapsedClass, value);
                _content.style.display = value ? DisplayStyle.None : DisplayStyle.Flex;
                CollapsedChanged?.Invoke(value);
            }
        }

        /// <summary>Raised when the section folds or unfolds, by click or by code.</summary>
        public event Action<bool> CollapsedChanged;
    }
}
