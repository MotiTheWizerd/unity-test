using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using Forge.EditorApp.UI.Controls;
using Forge.Editing;
using Forge.Editing.Palette;
using Forge.Editing.Terrain;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The assets browser along the bottom: a tab per top-level category (<see cref="PaletteTabs"/>) plus a Ground and a
    /// Materials tab, a flowing grid of tiles for the current tab (the sub-category is in the tooltip), and a search that
    /// spans every tab. The panel collapses to its header and its height can be dragged. Tiles are rebuilt only when what
    /// is shown changes. Picking an object tile chooses what the placing tools lay (<see cref="EditorContext.SelectPlacement"/>);
    /// picking a ground tile chooses what the paint tool lays (<see cref="EditorContext.SelectPaintLayer"/>); picking a
    /// material dresses the selection's chosen slot in it (<see cref="EditorCommands.DressSelection"/>).
    /// </summary>
    public sealed class AssetsBrowser : IPanelSection
    {
        // A category tab's key is a category's first segment, which never holds the separator, so these can't clash.
        static readonly string GroundTab = CategoryPath.Separator + "ground";
        static readonly string MaterialsTab = CategoryPath.Separator + "materials";
        const string GroundTitle = "Ground";
        const string MaterialsTitle = "Materials";
        const string UncategorizedTitle = "Uncategorized";
        const string CollapsedClass = "is-collapsed";
        const string CollapseIconOpen = "chevron-down";
        const string CollapseIconClosed = "chevron-up";
        const float MinHeight = 140f;
        const float MaxHeight = 520f;
        const int ShortcutCount = 9;

        readonly EditorContext _context;
        readonly EditorCommands _commands;
        readonly IAssetThumbnails _thumbnails;
        readonly GroundLooks _groundLooks;
        readonly MaterialLooks _materialLooks;
        readonly VisualElement _panel;
        readonly VisualElement _resize;
        readonly TabBar _tabs;
        readonly TextField _search;
        readonly Label _count;
        readonly ScrollView _grid;
        readonly ForgeButton _collapse;
        readonly Dictionary<RecordId, AssetTile> _tiles = new Dictionary<RecordId, AssetTile>();
        readonly Dictionary<RecordId, AssetTile> _groundTiles = new Dictionary<RecordId, AssetTile>();
        readonly Dictionary<RecordId, AssetTile> _materialTiles = new Dictionary<RecordId, AssetTile>();

        ObjectPalette _source;
        IReadOnlyList<GroundLayerRecord> _ground;
        IReadOnlyList<MaterialRecord> _materials;
        string _query = string.Empty;
        string _shownQuery;
        string _shownTab;
        bool _collapsed;
        float? _expandedHeight;
        float _dragStartY;
        float _dragStartHeight;

        /// <param name="thumbnails">Pictures for the object tiles; null leaves the placeholder icons.</param>
        /// <param name="groundLooks">What the ground tiles' material balls wear; null (or no thumbnails) leaves the placeholder icons.</param>
        /// <param name="materialLooks">What the material tiles' balls wear; null (or no thumbnails) leaves the placeholder icons.</param>
        public AssetsBrowser(VisualElement root, EditorContext context, EditorCommands commands, IAssetThumbnails thumbnails, GroundLooks groundLooks, MaterialLooks materialLooks)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _thumbnails = thumbnails;
            _groundLooks = groundLooks;
            _materialLooks = materialLooks;
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
            // The ground layers are cached per world version, so a new list means the world changed.
            var ground = _context.GroundLayers;
            var groundChanged = !ReferenceEquals(ground, _ground) && !SameEntries(ground, _ground);
            _ground = ground;
            var materials = _context.Materials;
            var materialsChanged = !ReferenceEquals(materials, _materials) && !SameEntries(materials, _materials);
            _materials = materials;
            if (sourceChanged || groundChanged || materialsChanged)
                _tabs.SetTabs(Tabs());

            if (sourceChanged || groundChanged || materialsChanged || _query != _shownQuery || _tabs.Current != _shownTab)
            {
                _shownQuery = _query;
                _shownTab = _tabs.Current;
                Rebuild();
            }

            // What is being laid stays marked while the hand is only held for a moment (Space).
            var placing = ToolCatalog.Of(_context.ActiveTool).LaysPaletteObject;
            foreach (var pair in _tiles)
                PanelElements.SetActive(pair.Value, placing && pair.Key == _context.SelectedBaseId);
            var painting = _context.ActiveTool == ToolKind.Paint;
            var paintLayer = _context.PaintLayer;
            foreach (var pair in _groundTiles)
                PanelElements.SetActive(pair.Value, painting && pair.Key == paintLayer);
            // What the selection wears in the slot a ball would dress.
            if (_materialTiles.Count > 0)
            {
                var worn = _commands.SelectionMaterial(_commands.DressedSlot(_commands.SelectionSlots()));
                foreach (var pair in _materialTiles)
                    PanelElements.SetActive(pair.Value, pair.Key == worn);
            }
        }

        // A tab per category, then Ground and Materials when the world has them.
        (string Key, string Title)[] Tabs()
        {
            var tabs = PaletteTabs.From(_source).Select(t => (t.Category, TabTitle(t.Category)));
            if (_ground.Count > 0)
                tabs = tabs.Append((GroundTab, GroundTitle));
            if (_materials.Count > 0)
                tabs = tabs.Append((MaterialsTab, MaterialsTitle));
            return tabs.ToArray();
        }

        void Rebuild()
        {
            _grid.Clear();
            _tiles.Clear();
            _groundTiles.Clear();
            _materialTiles.Clear();

            var searching = _query.Trim().Length > 0;
            _tabs.SetDimmed(searching);

            // A search spans every tab, objects then ground then materials; otherwise the grid shows the current tab.
            IReadOnlyList<PaletteGroup> groups = Array.Empty<PaletteGroup>();
            IReadOnlyList<GroundLayerRecord> ground = Array.Empty<GroundLayerRecord>();
            IReadOnlyList<MaterialRecord> materials = Array.Empty<MaterialRecord>();
            if (searching)
            {
                groups = _source.Filter(_query).Groups;
                ground = GroundLayers.Filter(_ground, _query);
                materials = Materials.Filter(_materials, _query);
            }
            else if (_tabs.Current == GroundTab)
            {
                ground = _ground;
            }
            else if (_tabs.Current == MaterialsTab)
            {
                materials = _materials;
            }
            else
            {
                groups = PaletteTabs.From(_source).FirstOrDefault(t => t.Category == _tabs.Current)?.Groups ?? groups;
            }

            // One flowing grid, in display order (so sub-categories stay together); the tooltip names the category.
            var shownCount = 0;
            foreach (var group in groups)
            foreach (var record in group.Objects)
            {
                _grid.Add(CreateTile(record));
                shownCount++;
            }

            foreach (var layer in ground)
            {
                _grid.Add(CreateTile(layer));
                shownCount++;
            }

            foreach (var material in materials)
            {
                _grid.Add(CreateTile(material));
                shownCount++;
            }

            PanelElements.SetText(_count, CountText(searching, shownCount));
        }

        static string CountText(int shown, int total, string what) => shown != total ? $"{shown} of {total}" : $"{total} {what}";

        string CountText(bool searching, int shownCount) =>
            searching ? CountText(shownCount, _source.Count + _ground.Count + _materials.Count, "assets")
            : _tabs.Current == GroundTab ? CountText(shownCount, _ground.Count, "grounds")
            : _tabs.Current == MaterialsTab ? CountText(shownCount, _materials.Count, "materials")
            : CountText(shownCount, _source.Count, "objects");

        AssetTile CreateTile(BaseObjectRecord record)
        {
            var id = record.Id;
            // The number is the 1-9 shortcut, which follows the unfiltered palette.
            var index = _source.IndexOf(id);
            var shortcut = index >= 0 && index < ShortcutCount ? index + 1 : 0;
            var tile = new AssetTile(record.DisplayName, shortcut, () => _context.SelectPlacement(id)) { tooltip = Tooltip(record, shortcut) };
            if (_thumbnails != null)
                tile.SetThumbnail(_thumbnails.Get(record.AssetKey));
            _tiles.Add(id, tile);
            return tile;
        }

        AssetTile CreateTile(GroundLayerRecord layer)
        {
            var id = layer.Id;
            var tile = new AssetTile(layer.DisplayName, 0, () => _context.SelectPaintLayer(id))
            {
                tooltip = $"{layer.DisplayName}  <color=#9EA4AE>· {GroundTitle}</color>",
            };
            // A material ball, keyed by the look, so layers sharing a look share a picture.
            if (_thumbnails != null && _groundLooks != null)
                tile.SetThumbnail(_thumbnails.GetBall(layer.AssetKey, () => _groundLooks.CreatePreviewMaterial(id)));
            _groundTiles.Add(id, tile);
            return tile;
        }

        AssetTile CreateTile(MaterialRecord material)
        {
            var id = material.Id;
            var tile = new AssetTile(material.DisplayName, 0, () => _commands.DressSelection(id))
            {
                tooltip = $"{material.DisplayName}  <color=#9EA4AE>· {MaterialsTitle}: click to dress the selection's slot</color>",
            };
            // Ball pictures are keyed by look, so a material and a ground of one key can't share one.
            if (_thumbnails != null && _materialLooks != null)
                tile.SetThumbnail(_thumbnails.GetBall(material.AssetKey, () => _materialLooks.CreatePreviewMaterial(id)));
            _materialTiles.Add(id, tile);
            return tile;
        }

        static string Tooltip(BaseObjectRecord record, int shortcut)
        {
            var where = record.Category.Length == 0 ? UncategorizedTitle : FullLabel(record.Category);
            var key = shortcut > 0 ? $"  <color=#9EA4AE>{shortcut}</color>" : string.Empty;
            return $"{record.DisplayName}{key}  <color=#9EA4AE>· {where}</color>";
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

        static bool SameEntries(IReadOnlyList<GroundLayerRecord> a, IReadOnlyList<GroundLayerRecord> b) =>
            b != null && a.Count == b.Count && a.Zip(b, (x, y) => x.Id == y.Id && x.DisplayName == y.DisplayName && x.AssetKey == y.AssetKey).All(same => same);

        static bool SameEntries(IReadOnlyList<MaterialRecord> a, IReadOnlyList<MaterialRecord> b) =>
            b != null && a.Count == b.Count && a.Zip(b, (x, y) => x.Id == y.Id && x.DisplayName == y.DisplayName && x.AssetKey == y.AssetKey).All(same => same);
    }
}
