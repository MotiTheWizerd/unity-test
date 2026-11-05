using System;
using Forge.Data;
using Forge.Data.Records;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// One asset in the assets browser: a thumbnail, the name under it and, for the first nine objects, the number
    /// key that places it. Until prefab thumbnails are rendered, the thumbnail shows a placeholder icon.
    /// </summary>
    public sealed class AssetTile : Button
    {
        public const string Class = "forge-tile";
        const string ThumbClass = "forge-tile__thumb";
        const string NameClass = "forge-tile__name";
        const string KeyClass = "forge-tile__key";
        const string PlaceholderIcon = "box";

        public AssetTile(BaseObjectRecord record, int shortcut, Action onPick) : base(onPick)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            Id = record.Id;
            AddToClassList(Class);

            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList(ThumbClass);
            thumb.Add(Icons.Make(PlaceholderIcon));
            Add(thumb);

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
    }
}
