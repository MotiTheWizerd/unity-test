using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.EditorApp.UI.Controls;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The world's materials as a grid of balls (<see cref="MaterialBalls"/>). Clicking one dresses the selection's chosen
    /// slot in it (<see cref="EditorCommands.DressSelection"/>); dragging one onto a part in the world dresses that part
    /// (<see cref="EditorCommands.DropMaterial"/>); a click is also told (<see cref="Picked"/>, the preview card shows
    /// it). The ball of what the selection wears in the chosen slot is lit. Tiles
    /// are rebuilt only when the world's materials change.
    /// </summary>
    public sealed class MaterialLibrary
    {
        readonly EditorContext _context;
        readonly EditorCommands _commands;
        readonly MaterialBalls _balls;
        readonly ScrollView _grid;
        readonly Dictionary<RecordId, AssetTile> _tiles = new Dictionary<RecordId, AssetTile>();
        IReadOnlyList<MaterialRecord> _materials;

        public MaterialLibrary(VisualElement root, EditorContext context, EditorCommands commands, MaterialBalls balls)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _balls = balls ?? throw new ArgumentNullException(nameof(balls));
            _grid = PanelElements.Required<ScrollView>(root, "material-library");
        }

        /// <summary>Raised when a ball is clicked, before it dresses the selection.</summary>
        public event Action<RecordId> Picked;

        public void Refresh()
        {
            // The list is cached per world version, so a new one means the world changed; rebuild only if its entries did.
            var materials = _context.Materials;
            if (!ReferenceEquals(materials, _materials) && !SameEntries(materials, _materials))
                Rebuild(materials);
            _materials = materials;

            var worn = _commands.SelectionMaterial(_commands.DressedSlot(_commands.SelectionSlots()));
            foreach (var pair in _tiles)
                PanelElements.SetActive(pair.Value, pair.Key == worn);
        }

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
            b != null && a.Count == b.Count && a.Zip(b, (x, y) => x.Id == y.Id && x.DisplayName == y.DisplayName && x.AssetKey == y.AssetKey).All(same => same);
    }
}
