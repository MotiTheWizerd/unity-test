using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// One entry in the assets browser (an object to place, a ground to paint): a thumbnail, the name under it and,
    /// optionally, the number key that picks it. The thumbnail shows a placeholder icon until <see cref="SetThumbnail"/>
    /// gives it a picture.
    /// </summary>
    public sealed class AssetTile : Button
    {
        public const string Class = "forge-tile";
        const string ThumbClass = "forge-tile__thumb";
        const string PictureClass = "forge-tile__thumb--picture";
        const string NameClass = "forge-tile__name";
        const string KeyClass = "forge-tile__key";
        const string PlaceholderIcon = "box";

        readonly VisualElement _thumb;
        readonly VisualElement _placeholder;

        /// <param name="shortcut">The number key shown in the corner; 0 for none.</param>
        public AssetTile(string displayName, int shortcut, Action onPick) : base(onPick)
        {
            AddToClassList(Class);

            _thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            _thumb.AddToClassList(ThumbClass);
            _placeholder = Icons.Make(PlaceholderIcon);
            _thumb.Add(_placeholder);
            Add(_thumb);

            var name = new Label(displayName ?? string.Empty) { pickingMode = PickingMode.Ignore };
            name.AddToClassList(NameClass);
            Add(name);

            if (shortcut > 0)
            {
                var key = new Label(shortcut.ToString()) { pickingMode = PickingMode.Ignore };
                key.AddToClassList(KeyClass);
                Add(key);
            }
        }

        /// <summary>Shows a picture in place of the placeholder icon; null brings the placeholder back.</summary>
        public void SetThumbnail(Texture2D picture)
        {
            var hasPicture = picture != null;
            _thumb.style.backgroundImage = hasPicture ? new StyleBackground(picture) : StyleKeyword.Null;
            _thumb.EnableInClassList(PictureClass, hasPicture);
            PanelElements.SetVisible(_placeholder, !hasPicture);
        }
    }
}
