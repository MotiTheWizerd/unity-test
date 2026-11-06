using Forge.World.Assets;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// A big picture of one thing on a studio backdrop (<see cref="StudioBackdrop"/>), as material editors show the
    /// material they're on: the picture stands on the backdrop's floor, its name and a caption sit in the top-left
    /// corner. With no picture it shows a placeholder icon. Styled by PreviewCard.uss.
    /// </summary>
    [UxmlElement]
    public partial class PreviewCard : VisualElement
    {
        public const string Class = "forge-preview-card";
        const string PictureClass = "forge-preview-card__picture";
        const string TitleClass = "forge-preview-card__title";
        const string CaptionClass = "forge-preview-card__caption";
        const string PlaceholderIcon = "palette";
        const int BackdropHeight = 256;

        // One backdrop for every card; made on first use, and again if the old one was destroyed.
        static Texture2D _backdrop;

        readonly VisualElement _picture;
        readonly VisualElement _placeholder;
        readonly Label _title;
        readonly Label _caption;

        public PreviewCard()
        {
            AddToClassList(Class);
            pickingMode = PickingMode.Ignore;
            if (_backdrop == null)
                _backdrop = StudioBackdrop.Create(BackdropHeight);
            style.backgroundImage = new StyleBackground(_backdrop);

            _picture = new VisualElement { name = "picture", pickingMode = PickingMode.Ignore };
            _picture.AddToClassList(PictureClass);
            _placeholder = Icons.Make(PlaceholderIcon);
            _picture.Add(_placeholder);
            Add(_picture);

            _title = new Label { name = "title", pickingMode = PickingMode.Ignore };
            _title.AddToClassList(TitleClass);
            Add(_title);

            _caption = new Label { name = "caption", pickingMode = PickingMode.Ignore };
            _caption.AddToClassList(CaptionClass);
            Add(_caption);
        }

        /// <summary>Shows <paramref name="picture"/> (null: the placeholder icon) named <paramref name="title"/>, with a caption under it (null: none).</summary>
        public void Show(Texture2D picture, string title, string caption)
        {
            _picture.style.backgroundImage = picture != null ? new StyleBackground(picture) : StyleKeyword.Null;
            PanelElements.SetVisible(_placeholder, picture == null);
            PanelElements.SetText(_title, title ?? string.Empty);
            PanelElements.SetText(_caption, caption ?? string.Empty);
            PanelElements.SetVisible(_caption, !string.IsNullOrEmpty(caption));
        }
    }
}
