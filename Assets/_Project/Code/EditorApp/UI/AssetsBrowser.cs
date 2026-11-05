using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using Forge.EditorApp.UI.Controls;
using Forge.Editing.Palette;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The assets browser along the bottom: a tab per top-level category (<see cref="PaletteTabs"/>), a grid of
    /// tiles for the current tab with a small header per sub-category, and a search that spans every tab. The panel
    /// collapses to its header and its height can be dragged. Tiles are rebuilt only when the shown objects change.
    /// Picking a tile chooses the object the placing tools lay (<see cref="EditorContext.SelectPlacement"/>).
    /// </summary>
    public sealed class AssetsBrowser : IPanelSection
    {
        const string UncategorizedTitle = "Uncategorized";
        const string CollapsedClass = "is-collapsed";
        const string GroupClass = "forge-assets__group";
        const string CollapseIconOpen = "chevron-down";
        const string CollapseIconClosed = "chevron-up";
        const float MinHeight = 140f;
        const float MaxHeight = 520f;
        const int ShortcutCount = 9;

        readonly EditorContext _context;
        readonly VisualElement _panel;
        readonly VisualElement _resize;
        readonly TabBar _tabs;
        readonly TextField _search;
        readonly Label _count;
        readonly ScrollView _grid;
        readonly ForgeButton _collapse;
        readonly Dictionary<RecordId, AssetTile> _tiles = new Dictionary<RecordId, AssetTile>();

        ObjectPalette _source;
        string _query = string.Empty;
        string _shownQuery;
        string _shownTab;
        bool _collapsed;
        float? _expandedHeight;
        float _dragStartY;
        float _dragStartHeight;

        public AssetsBrowser(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _panel = PanelElements.Required<VisualElement>(root, "assets");
            _resize = PanelElements.Required<VisualElement>(root, "assets-resize");
            _search = PanelElements.Required<TextField>(root, "assets-search");
            _count = PanelElements.Required<Label>(root, "assets-count");
            _grid = PanelElements.Required<ScrollView>(root, "assets-grid");

            _tabs = new TabBar();
            PanelElements.Required<VisualElement>(root, "assets-tabs").Add(_tabs);

            _collapse = PanelElements.Required<ForgeButton>(root, "assets-collapse");
            _collapse.icon = CollapseIconOpen;
            _collapse.clicked += () => SetCollapsed(!_collapsed);

            _search.RegisterValueChangedCallback(e => _query = e.newValue ?? string.Empty);
            // Esc clears the search (EditorPanel also releases the focus on Esc).
            _search.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape)
                    _search.value = string.Empty;
            }, TrickleDown.TrickleDown);

            RegisterResize();
        }

        public void Refresh()
        {
            // The palette is a new instance after every edit; rebuild only when its entries, the query or the tab changed.
            var source = _context.Palette;
            var sourceChanged = !ReferenceEquals(source, _source) && !SameEntries(source, _source);
            _source = source;
            if (sourceChanged)
                _tabs.SetTabs(PaletteTabs.From(source).Select(t => (t.Category, TabTitle(t.Category))).ToArray());

            if (sourceChanged || _query != _shownQuery || _tabs.Current != _shownTab)
            {
                _shownQuery = _query;
                _shownTab = _tabs.Current;
                Rebuild();
            }

            // The object being laid stays marked while the hand is only held for a moment (Space).
            var placing = ToolCatalog.Of(_context.ActiveTool).LaysPaletteObject;
            foreach (var pair in _tiles)
                PanelElements.SetActive(pair.Value, placing && pair.Key == _context.SelectedBaseId);
        }

        void Rebuild()
        {
            _grid.Clear();
            _tiles.Clear();

            var searching = _query.Trim().Length > 0;
            _tabs.SetDimmed(searching);

            // A search spans every tab; otherwise the grid shows the current tab's groups.
            IReadOnlyList<PaletteGroup> groups;
            int shownCount;
            if (searching)
            {
                var matches = _source.Filter(_query);
                groups = matches.Groups;
                shownCount = matches.Count;
            }
            else
            {
                var tab = PaletteTabs.From(_source).FirstOrDefault(t => t.Category == _tabs.Current);
                groups = tab?.Groups ?? Array.Empty<PaletteGroup>();
                shownCount = tab?.Count ?? 0;
            }

            // Sub-headers only where they tell something: a search shows full paths; a tab shows what is under it,
            // and objects sitting directly in the tab's category come first without a header.
            foreach (var group in groups)
            {
                var header = searching ? FullLabel(group.Category) : CategoryPath.Tail(group.Category);
                if (searching || header.Length > 0)
                    _grid.Add(GroupHeader(header));
                foreach (var record in group.Objects)
                    _grid.Add(CreateTile(record));
            }

            PanelElements.SetText(_count, searching || shownCount != _source.Count
                ? $"{shownCount} of {_source.Count}"
                : $"{_source.Count} objects");
        }

        AssetTile CreateTile(BaseObjectRecord record)
        {
            var id = record.Id;
            // The number is the 1-9 shortcut, which follows the unfiltered palette.
            var index = _source.IndexOf(id);
            var shortcut = index >= 0 && index < ShortcutCount ? index + 1 : 0;
            var tile = new AssetTile(record, shortcut, () => _context.SelectPlacement(id)) { tooltip = Tooltip(record, shortcut) };
            _tiles.Add(id, tile);
            return tile;
        }

        static string Tooltip(BaseObjectRecord record, int shortcut)
        {
            var where = record.Category.Length == 0 ? UncategorizedTitle : FullLabel(record.Category);
            var key = shortcut > 0 ? $"  <color=#9EA4AE>{shortcut}</color>" : string.Empty;
            return $"{record.DisplayName}{key}  <color=#9EA4AE>· {where}</color>";
        }

        static Label GroupHeader(string text)
        {
            var header = new Label(text.Length == 0 ? UncategorizedTitle : text) { pickingMode = PickingMode.Ignore };
            header.AddToClassList(GroupClass);
            return header;
        }

        static string TabTitle(string category) => category.Length == 0 ? UncategorizedTitle : category;

        static string FullLabel(string category) => category.Replace(CategoryPath.Separator.ToString(), " / ");

        void SetCollapsed(bool collapsed)
        {
            if (_collapsed == collapsed)
                return;
            _collapsed = collapsed;
            _panel.EnableInClassList(CollapsedClass, collapsed);
            _collapse.icon = collapsed ? CollapseIconClosed : CollapseIconOpen;

            // The dragged height is inline, so it is lifted while collapsed and restored after.
            if (collapsed)
            {
                if (_expandedHeight.HasValue)
                    _panel.style.height = StyleKeyword.Null;
            }
            else if (_expandedHeight.HasValue)
            {
                _panel.style.height = _expandedHeight.Value;
            }
        }

        // The top edge is a handle: dragging it up makes the browser taller.
        void RegisterResize()
        {
            _resize.RegisterCallback<PointerDownEvent>(e =>
            {
                if (_collapsed || e.button != 0)
                    return;
                _dragStartY = e.position.y;
                _dragStartHeight = _panel.resolvedStyle.height;
                _resize.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            _resize.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_resize.HasPointerCapture(e.pointerId))
                    return;
                var height = Mathf.Clamp(_dragStartHeight + (_dragStartY - e.position.y), MinHeight, MaxHeight);
                _expandedHeight = height;
                _panel.style.height = height;
            });
            _resize.RegisterCallback<PointerUpEvent>(e =>
            {
                if (_resize.HasPointerCapture(e.pointerId))
                    _resize.ReleasePointer(e.pointerId);
            });
        }

        static bool SameEntries(ObjectPalette a, ObjectPalette b) =>
            b != null && a.Count == b.Count && a.Objects.Zip(b.Objects, Same).All(same => same);

        static bool Same(BaseObjectRecord x, BaseObjectRecord y) =>
            x.Id == y.Id && x.DisplayName == y.DisplayName && x.Category == y.Category;
    }
}
