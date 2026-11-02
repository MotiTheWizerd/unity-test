using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using Forge.Editing.Palette;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The object palette: a search field and one foldout per category, each with a button per
    /// base object. The buttons are rebuilt only when the shown objects change, not every frame.
    /// </summary>
    public sealed class PaletteSection : IPanelSection
    {
        const string UncategorizedLabel = "Uncategorized";
        const string ButtonsClass = "forge-palette";
        const string GroupClass = "forge-palette-group";

        readonly EditorContext _context;
        readonly TextField _search;
        readonly Label _count;
        readonly VisualElement _placementOptions;
        readonly Label _rotation;
        readonly Toggle _randomize;
        readonly Toggle _alignToSlope;
        readonly VisualElement _strokeOptions;
        readonly Slider _gap;
        readonly Slider _gapRandom;
        readonly VisualElement _container;
        readonly Dictionary<RecordId, Button> _buttons = new Dictionary<RecordId, Button>();

        // Categories the user folded; kept across rebuilds. A search shows every match regardless.
        readonly HashSet<string> _collapsed = new HashSet<string>(StringComparer.Ordinal);

        ObjectPalette _source;
        string _query = string.Empty;
        string _shownQuery;

        public PaletteSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _search = PanelElements.Required<TextField>(root, "palette-search");
            _count = PanelElements.Required<Label>(root, "palette-count");
            _container = PanelElements.Required<VisualElement>(root, "palette");
            _placementOptions = PanelElements.Required<VisualElement>(root, "placement-options");
            _rotation = PanelElements.Required<Label>(root, "placement-rotation");
            PanelElements.Required<Button>(root, "rotate-left").clicked += () => _context.RotatePlacement(-EditorContext.PlacementStep);
            PanelElements.Required<Button>(root, "rotate-right").clicked += () => _context.RotatePlacement(EditorContext.PlacementStep);
            _randomize = PanelElements.Required<Toggle>(root, "placement-randomize");
            _randomize.RegisterValueChangedCallback(e => _context.RandomizePlacement = e.newValue);
            _alignToSlope = PanelElements.Required<Toggle>(root, "placement-align-slope");
            _alignToSlope.RegisterValueChangedCallback(e => _context.AlignPlacementToSlope = e.newValue);
            _strokeOptions = PanelElements.Required<VisualElement>(root, "stroke-options");
            _gap = GapSlider(root, "placement-gap", value => _context.PlacementGap = value);
            _gapRandom = GapSlider(root, "placement-gap-random", value => _context.PlacementGapRandom = value);
            _search.RegisterValueChangedCallback(e => _query = e.newValue ?? string.Empty);

            // Esc clears the search (EditorPanel also releases the focus on Esc).
            _search.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape)
                    _search.value = string.Empty;
            }, TrickleDown.TrickleDown);
        }

        public void Refresh()
        {
            // The palette is a new instance after every edit; rebuild only when its entries or the query changed.
            var source = _context.Palette;
            var sourceChanged = !ReferenceEquals(source, _source) && !SameEntries(source, _source);
            _source = source;
            if (sourceChanged || _query != _shownQuery)
            {
                _shownQuery = _query;
                var shown = source.Filter(_query);
                Rebuild(shown);
                PanelElements.SetText(_count, ReferenceEquals(shown, source)
                    ? $"{source.Count} objects"
                    : $"{shown.Count} of {source.Count} objects");
            }

            var placing = _context.ActiveTool == ToolKind.Place;
            PanelElements.SetVisible(_placementOptions, placing);
            PanelElements.SetVisible(_randomize, placing);
            PanelElements.SetVisible(_alignToSlope, placing);
            if (placing)
            {
                var roll = _context.PlacementRoll;
                var varies = _context.PlacementCanVary;
                PanelElements.SetText(_rotation, varies && _context.RandomizePlacement
                    ? $"Rotation {_context.PlacementYaw:0}° + {roll.YawDegrees:0}°, size ×{roll.Scale:0.00}"
                    : $"Rotation {_context.PlacementYaw:0}°");
                _randomize.SetValueWithoutNotify(_context.RandomizePlacement);
                // Objects without variation (houses, walls) are always placed exactly as chosen.
                _randomize.SetEnabled(varies);
                // Upright things (trees, houses) never tilt, so the toggle only matters for fences, walls, rocks.
                _alignToSlope.SetValueWithoutNotify(_context.AlignPlacementToSlope);
                _alignToSlope.SetEnabled(_context.PlacementCanAlignToSlope);
                // Gaps are remembered per object, so switching objects changes what the sliders show.
                Mirror(_gap, _context.PlacementGap);
                Mirror(_gapRandom, _context.PlacementGapRandom);
            }

            PanelElements.SetVisible(_strokeOptions, placing);
            foreach (var pair in _buttons)
                PanelElements.SetActive(pair.Value, placing && pair.Key == _context.SelectedBaseId);
        }

        void Rebuild(ObjectPalette shown)
        {
            _container.Clear();
            _buttons.Clear();
            var searching = _query.Trim().Length > 0;

            foreach (var group in shown.Groups)
            {
                var category = group.Category;
                var foldout = new Foldout
                {
                    text = $"{CategoryLabel(category)}  ({group.Objects.Count})",
                    value = searching || !_collapsed.Contains(category),
                };
                foldout.AddToClassList(GroupClass);
                foldout.RegisterValueChangedCallback(e =>
                {
                    // Only the foldout's own toggle, not value changes bubbling from children.
                    if (e.target != foldout || searching)
                        return;
                    if (e.newValue)
                        _collapsed.Remove(category);
                    else
                        _collapsed.Add(category);
                });

                var buttons = new VisualElement();
                buttons.AddToClassList(ButtonsClass);
                foreach (var record in group.Objects)
                    buttons.Add(CreateButton(record));

                foldout.Add(buttons);
                _container.Add(foldout);
            }
        }

        Button CreateButton(BaseObjectRecord record)
        {
            var id = record.Id;
            var index = _source.IndexOf(id);
            var button = new Button(() => _context.SelectPlacement(id))
            {
                // The number is the 1-9 shortcut, which follows the unfiltered palette.
                text = index >= 0 && index < 9 ? $"{index + 1}. {record.DisplayName}" : record.DisplayName,
                tooltip = id.ToString(),
            };
            _buttons.Add(id, button);
            return button;
        }

        static Slider GapSlider(VisualElement root, string name, Action<float> set)
        {
            var slider = PanelElements.Required<Slider>(root, name);
            slider.lowValue = 0f;
            slider.highValue = EditorContext.MaxPlacementGap;
            slider.RegisterValueChangedCallback(e => set(e.newValue));
            return slider;
        }

        static void Mirror(Slider slider, float value)
        {
            if (!Mathf.Approximately(slider.value, value))
                slider.SetValueWithoutNotify(value);
        }

        static string CategoryLabel(string category) =>
            category.Length == 0 ? UncategorizedLabel : category.Replace(CategoryPath.Separator.ToString(), " / ");

        static bool SameEntries(ObjectPalette a, ObjectPalette b) =>
            b != null && a.Count == b.Count && a.Objects.Zip(b.Objects, Same).All(same => same);

        static bool Same(BaseObjectRecord x, BaseObjectRecord y) =>
            x.Id == y.Id && x.DisplayName == y.DisplayName && x.Category == y.Category;
    }
}
