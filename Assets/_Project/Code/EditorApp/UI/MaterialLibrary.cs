using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.EditorApp.UI.Controls;
using Forge.Editing;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The world's materials as a grid of balls (<see cref="MaterialBalls"/>), under tabs: All, then one per kind of
    /// surface the materials name in their data (<see cref="Materials.Kinds"/>), so a new kind brings its own tab with
    /// no code. Clicking one dresses the selection's chosen
    /// slot in it (<see cref="EditorCommands.DressSelection"/>); dragging one onto a part in the world dresses that part
    /// (<see cref="EditorCommands.DropMaterial"/>); a click is also told (<see cref="Picked"/>, the preview card shows
    /// it). The ball of what the selection wears in the chosen slot is lit. Tiles
    /// are rebuilt only when the world's materials or the tab change.
    /// </summary>
    public sealed class MaterialLibrary
    {
        readonly EditorContext _context;
        readonly EditorCommands _commands;
        readonly MaterialBalls _balls;
        // A kind is a category's first segment, which never holds the separator, so this can't clash.
        static readonly string AllTab = CategoryPath.Separator + "all";
        const string AllTitle = "All";

        readonly ScrollView _grid;
        readonly TabBar _tabs = new TabBar();
        readonly Dictionary<RecordId, AssetTile> _tiles = new Dictionary<RecordId, AssetTile>();
        IReadOnlyList<MaterialRecord> _materials;
        string _shownTab;

        public MaterialLibrary(VisualElement root, EditorContext context, EditorCommands commands, MaterialBalls balls)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _balls = balls ?? throw new ArgumentNullException(nameof(balls));
            _grid = PanelElements.Required<ScrollView>(root, "material-library");
            PanelElements.Required<VisualElement>(root, "material-tabs").Add(_tabs);
        }

        /// <summary>Raised when a ball is clicked, before it dresses the selection.</summary>
        public event Action<RecordId> Picked;

        public void Refresh()
        {
            // The list is cached per world version, so a new one means the world changed; rebuild only if its entries did.
            var materials = _context.Materials;
            var changed = !ReferenceEquals(materials, _materials) && !SameEntries(materials, _materials);
            _materials = materials;
            if (changed)
                _tabs.SetTabs(Tabs(materials));
            if (changed || _tabs.Current != _shownTab)
            {
                _shownTab = _tabs.Current;
                Rebuild(Materials.OfKind(materials, _shownTab == AllTab ? null : _shownTab));
            }

            var worn = _commands.SelectionMaterial(_commands.DressedSlot(_commands.SelectionSlots()));
            foreach (var pair in _tiles)
                PanelElements.SetActive(pair.Value, pair.Key == worn);
        }

        static (string Key, string Title)[] Tabs(IReadOnlyList<MaterialRecord> materials) =>
            new[] { (AllTab, AllTitle) }.Concat(Materials.Kinds(materials).Select(kind => (kind, kind))).ToArray();

        void Rebuild(IReadOnlyList<MaterialRecord> materials)
        {
            _grid.Clear();
            _tiles.Clear();
            foreach (var material in materials)
            {
                var tile = CreateTile(material);
                _grid.Add(tile);
                _tiles.Add(material.Id, tile);
            }
        }

        AssetTile CreateTile(MaterialRecord material)
        {
            var id = material.Id;
            var tile = new AssetTile(material.DisplayName, 0, () =>
            {
                Picked?.Invoke(id);
                _commands.DressSelection(id);
            })
            {
                tooltip = $"{material.DisplayName}  <color=#9EA4AE>· click: dress the chosen part · drag onto any part</color>",
            };
            tile.SetThumbnail(_balls.Of(material));
            tile.AddManipulator(TileDrags.Of(_context, AssetDragKind.Material, id, () => _commands.DropMaterial(id)));
            return tile;
        }

        static bool SameEntries(IReadOnlyList<MaterialRecord> a, IReadOnlyList<MaterialRecord> b) =>
            b != null && a.Count == b.Count
            && a.Zip(b, (x, y) => x.Id == y.Id && x.DisplayName == y.DisplayName && x.AssetKey == y.AssetKey && x.Category == y.Category).All(same => same);
    }
}
