using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Spaces;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The space being shown and edited (a picker of every worldspace and interior), a worldspace's sea level and
    /// climate, and New… to create a space: a worldspace of a chosen size of flat land, or an empty interior. Creating one switches to it.
    /// </summary>
    public sealed class SpaceSection : IPanelSection
    {
        /// <summary>How far below the land a sea starts when it is switched on, so turning it on floods nothing.</summary>
        const float NewSeaBelowLand = 2f;

        /// <summary>The climate picker's first choice: no climate, the same day all year.</summary>
        const string NoClimate = "None (same day all year)";

        readonly EditorContext _context;
        readonly DropdownField _picker;
        readonly VisualElement _seaRow;
        readonly Toggle _seaOn;
        readonly FloatField _seaLevel;
        readonly DropdownField _climate;
        readonly Button _new;
        readonly VisualElement _form;
        readonly TextField _name;
        readonly ChoiceRow<SpaceKind> _kinds;
        readonly VisualElement _sizeRow;
        readonly Dictionary<int, Button> _sizes = new Dictionary<int, Button>();
        List<SpaceRecord> _spaces = new List<SpaceRecord>();
        List<ClimateRecord> _climates = new List<ClimateRecord>();
        object _shownWorld;
        SpaceKind _kind = SpaceKind.Worldspace;
        int _size = SpaceEdits.Sizes[0];

        public SpaceSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = PanelElements.Required<DropdownField>(root, "space-picker");
            _picker.RegisterValueChangedCallback(_ => ShowPicked());
            _seaRow = PanelElements.Required<VisualElement>(root, "space-sea");
            _seaOn = PanelElements.Required<Toggle>(root, "space-sea-on");
            _seaOn.RegisterValueChangedCallback(change => SetSea(change.newValue ? NewSeaLevel() : (float?)null));
            _seaLevel = PanelElements.Required<FloatField>(root, "space-sea-level");
            _seaLevel.RegisterValueChangedCallback(change => SetSea(change.newValue));
            _climate = PanelElements.Required<DropdownField>(root, "space-climate");
            _climate.RegisterValueChangedCallback(_ => SetClimate());
            _new =PanelElements.Required<Button>(root, "space-new");
            _new.clicked += () => SetFormOpen(!IsFormOpen);

            _form = PanelElements.Required<VisualElement>(root, "space-new-form");
            _name = PanelElements.Required<TextField>(root, "space-new-name");
            _kinds = new ChoiceRow<SpaceKind>(PanelElements.Required<VisualElement>(root, "space-new-kinds"), kind => kind.ToString(), kind => _kind = kind);
            _sizeRow = PanelElements.Required<VisualElement>(root, "space-new-sizes");
            _sizeRow.Clear();
            foreach (var size in SpaceEdits.Sizes)
            {
                var button = new Button(() => _size = size) { text = SizeLabel(size) };
                _sizeRow.Add(button);
                _sizes.Add(size, button);
            }

            PanelElements.Required<Button>(root, "space-create").clicked += Create;
            PanelElements.Required<Button>(root, "space-cancel").clicked += () => SetFormOpen(false);
            SetFormOpen(false);
        }

        public void Refresh()
        {
            RefreshPicker();
            RefreshSea();
            RefreshClimate();
            if (!IsFormOpen)
                return;

            _kinds.Refresh(_kind);
            PanelElements.SetVisible(_sizeRow, _kind == SpaceKind.Worldspace);
            foreach (var pair in _sizes)
                PanelElements.SetActive(pair.Value, pair.Key == _size);
        }

        bool IsFormOpen => !_form.ClassListContains(PanelElements.HiddenClass);

        void SetFormOpen(bool open)
        {
            PanelElements.SetVisible(_form, open);
            PanelElements.SetActive(_new, open);
            if (open)
                _name.value = string.Empty;
        }

        // The choices change only with the world; the shown value follows the active space.
        void RefreshPicker()
        {
            var world = _context.Session.World;
            if (!ReferenceEquals(world, _shownWorld))
            {
                _shownWorld = world;
                _spaces = world.Records.OfType<SpaceRecord>()
                    .OrderBy(s => s.Kind)
                    .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.Id)
                    .ToList();
                _picker.choices = _spaces.Select(Label).ToList();
                _climates = world.Records.OfType<ClimateRecord>()
                    .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.Id)
                    .ToList();
                _climate.choices = new[] { NoClimate }.Concat(_climates.Select(c => c.DisplayName)).ToList();
            }

            var active = _spaces.FindIndex(s => s.Id == _context.ActiveSpace);
            if (active >= 0 && _picker.index != active)
                _picker.SetValueWithoutNotify(_picker.choices[active]);
        }

        // Mirrors the space's record, which undo and the agent change too. A value being typed isn't the field's
        // value until it is committed (the field is delayed), so it is never overwritten mid-edit.
        void RefreshSea()
        {
            var space = _context.ActiveSpaceRecord;
            var worldspace = space != null && space.Kind == SpaceKind.Worldspace;
            PanelElements.SetVisible(_seaRow, worldspace);
            if (!worldspace)
                return;

            var level = space.WaterLevel;
            _seaOn.SetValueWithoutNotify(level.HasValue);
            _seaLevel.SetEnabled(level.HasValue);
            if (level.HasValue && _seaLevel.value != level.Value)
                _seaLevel.SetValueWithoutNotify(level.Value);
        }

        // Mirrors the space's climate, which undo and the agent change too; one the loaded plugins lack shows as missing.
        void RefreshClimate()
        {
            var space = _context.ActiveSpaceRecord;
            var worldspace = space != null && space.Kind == SpaceKind.Worldspace;
            PanelElements.SetVisible(_climate, worldspace);
            if (!worldspace)
                return;

            var shown = space.Climate is RecordId id
                ? _climates.FirstOrDefault(c => c.Id == id)?.DisplayName ?? $"Missing: {id}"
                : NoClimate;
            if (_climate.value != shown)
                _climate.SetValueWithoutNotify(shown);
        }

        void SetClimate()
        {
            var index = _climate.index;
            if (index < 0)
                return;

            var climate = index == 0 ? (RecordId?)null : _climates[index - 1].Id;
            var changed = false;
            if (_context.TryEdit("Climate", () => changed = SpaceEdits.SetClimate(_context.Session, _context.ActiveSpace, climate)) && changed)
                _context.Report(_context.Session.UndoDescription);
        }

        float NewSeaLevel() => Math.Max(0f, (_context.ActiveSpaceRecord?.BaseHeight ?? 0f) - NewSeaBelowLand);

        void SetSea(float? level)
        {
            var clamped = level.HasValue ? Math.Max(0f, Math.Min(WorldGrid.MaxTerrainHeight, level.Value)) : (float?)null;
            var changed = false;
            if (_context.TryEdit("Sea level", () => changed = SpaceEdits.SetWaterLevel(_context.Session, _context.ActiveSpace, clamped)) && changed)
                _context.Report(_context.Session.UndoDescription);
        }

        void ShowPicked()
        {
            var index = _picker.index;
            if (index >= 0 && index < _spaces.Count && !_context.ShowSpace(_spaces[index].Id))
                RefreshPicker();
        }

        void Create()
        {
            var name = _name.value;
            if (string.IsNullOrWhiteSpace(name))
            {
                _context.Report("Give the new space a name.");
                return;
            }

            var id = default(RecordId);
            var created = _context.TryEdit("New space", () => id = _kind == SpaceKind.Interior
                ? SpaceEdits.CreateInterior(_context.Session, name)
                : SpaceEdits.CreateWorldspace(_context.Session, name, _size, WorldGrid.DefaultLandHeight));
            if (!created)
                return;

            SetFormOpen(false);
            _context.ShowSpace(id);
        }

        static string Label(SpaceRecord space) =>
            space.Kind == SpaceKind.Interior ? $"{space.DisplayName}  (interior)" : space.DisplayName;

        static string SizeLabel(int size) => $"{size}×{size}  ({size * WorldGrid.ChunkSize:0} m)";
    }
}
