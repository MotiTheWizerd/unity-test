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
    /// The selection's material slots (<see cref="EditorCommands.SelectionSlots"/>), a <see cref="MaterialPartRow"/> each,
    /// showing what it wears. Clicking a row chooses the slot a material dresses (<see cref="EditorContext.MaterialSlot"/>,
    /// so its parts glow); the chosen one is lit. Below them, Reset look gives the selection back its own materials. With
    /// no slots selected, a hint says what to do. It builds itself into the element it is given, so the Materials page
    /// and the inspector each show one.
    /// </summary>
    public sealed class MaterialSlotList
    {
        const string OwnLook = "Own look";
        const string SelectHint = "Select an object to dress its parts, or drag a ball onto any part.";
        const string NoSlotsHint = "The selection has no parts to dress.";
        const string RowsClass = "forge-material-slots";

        readonly EditorContext _context;
        readonly EditorCommands _commands;
        readonly MaterialBalls _balls;
        readonly VisualElement _rows;
        readonly Label _hint;
        readonly Button _reset;
        readonly List<MaterialPartRow> _shown = new List<MaterialPartRow>();
        readonly Dictionary<string, RecordId?> _worn = new Dictionary<string, RecordId?>(StringComparer.Ordinal);
        IReadOnlyList<MaterialRecord> _materials;

        /// <param name="host">The element the list builds itself into.</param>
        public MaterialSlotList(VisualElement host, EditorContext context, EditorCommands commands, MaterialBalls balls)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _balls = balls ?? throw new ArgumentNullException(nameof(balls));

            _hint = new Label();
            _hint.AddToClassList("forge-muted");
            _hint.AddToClassList("forge-wrap");
            host.Add(_hint);

            _rows = new VisualElement();
            _rows.AddToClassList(RowsClass);
            host.Add(_rows);

            _reset = new ForgeButton(commands.ResetSelectionMaterials)
            {
                text = "Reset look",
                tooltip = "Give the selection back its own materials in every part",
            };
            host.Add(_reset);
        }

        public void Refresh()
        {
            var slots = _commands.SelectionSlots();
            var hasSlots = slots.Count > 0;
            PanelElements.SetVisible(_hint, !hasSlots);
            PanelElements.SetVisible(_reset, hasSlots);
            if (!hasSlots)
                PanelElements.SetText(_hint, _context.Selection.IsEmpty ? SelectHint : NoSlotsHint);

            if (!slots.SequenceEqual(_shown.Select(row => row.Slot)))
                Rebuild(slots);

            var chosen = _commands.DressedSlot(slots);
            // Other materials (a new world version) may rename or restyle what a row wears.
            var materials = _context.Materials;
            if (!ReferenceEquals(materials, _materials))
            {
                _materials = materials;
                _worn.Clear();
            }

            foreach (var row in _shown)
            {
                PanelElements.SetActive(row, row.Slot == chosen);
                var worn = _commands.SelectionMaterial(row.Slot);
                // A row's ball changes only when what it wears does.
                if (_worn.TryGetValue(row.Slot, out var was) && was == worn)
                    continue;
                _worn[row.Slot] = worn;
                var material = worn is RecordId id ? materials.FirstOrDefault(m => m.Id == id) : null;
                row.Show(material?.DisplayName ?? OwnLook, material != null ? _balls.Of(material) : null);
            }
        }

        void Rebuild(IReadOnlyList<string> slots)
        {
            _rows.Clear();
            _shown.Clear();
            _worn.Clear();
            foreach (var slot in slots)
            {
                var row = new MaterialPartRow(slot, Materials.SlotLabel(slot), picked => _context.MaterialSlot = picked);
                _rows.Add(row);
                _shown.Add(row);
            }
        }
    }
}
