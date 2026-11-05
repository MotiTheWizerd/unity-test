using System;
using Forge.Data;
using Forge.Data.Records;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// One asset in the assets browser: a thumbnail, the name under it and, for the first nine objects, the number
    /// key that places it. The thumbnail shows a placeholder icon until <see cref="SetThumbnail"/> gives it a picture.
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

        public AssetTile(BaseObjectRecord record, int shortcut, Action onPick) : base(onPick)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            Id = record.Id;
            AssetKey = record.AssetKey;
            AddToClassList(Class);

            _thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            _thumb.AddToClassList(ThumbClass);
            _placeholder = Icons.Make(PlaceholderIcon);
            _thumb.Add(_placeholder);
            Add(_thumb);

            var name = new Label(record.DisplayName) { pickingMode = PickingMode.Ignore };
            name.AddToClassList(NameClass);
            Add(name);

            if (shortcut > 0)
            {
                var key = new Label(shortcut.ToString()) { pickingMode = PickingMode.Ignore };
                key.AddToClassList(KeyClass);
                Add(key);
            }
        }

        public RecordId Id { get; }

        /// <summary>The asset key the record presents with; what a thumbnail is rendered from.</summary>
        public string AssetKey { get; }

        /// <summary>Shows a rendered picture in place of the placeholder icon; null brings the placeholder back.</summary>
        public void SetThumbnail(Texture2D picture)
        {
            var hasPicture = picture != null;
            _thumb.style.backgroundImage = hasPicture ? new StyleBackground(picture) : StyleKeyword.Null;
            _thumb.EnableInClassList(PictureClass, hasPicture);
            PanelElements.SetVisible(_placeholder, !hasPicture);
        }
    }
}
