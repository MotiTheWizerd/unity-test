using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>One button per base object; the buttons are rebuilt only when the palette itself changes.</summary>
    public sealed class PaletteSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _container;
        readonly Dictionary<RecordId, Button> _buttons = new Dictionary<RecordId, Button>();
        IReadOnlyList<BaseObjectRecord> _shown;

        public PaletteSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _container = PanelElements.Required<VisualElement>(root, "palette");
        }

        public void Refresh()
        {
            var palette = _context.Palette;
            if (!ReferenceEquals(palette, _shown) && !SameEntries(palette, _shown))
                Rebuild(palette);
            _shown = palette;

            var placing = _context.ActiveTool == ToolKind.Place;
            foreach (var pair in _buttons)
                PanelElements.SetActive(pair.Value, placing && pair.Key == _context.SelectedBaseId);
        }

        void Rebuild(IReadOnlyList<BaseObjectRecord> palette)
        {
            _container.Clear();
            _buttons.Clear();
            for (var i = 0; i < palette.Count; i++)
            {
                var id = palette[i].Id;
                var button = new Button(() => _context.SelectPlacement(id))
                {
                    text = i < 9 ? $"{i + 1}. {palette[i].DisplayName}" : palette[i].DisplayName,
                };
                _container.Add(button);
                _buttons.Add(id, button);
            }
        }

        static bool SameEntries(IReadOnlyList<BaseObjectRecord> a, IReadOnlyList<BaseObjectRecord> b) =>
            b != null && a.Count == b.Count && a.Zip(b, (x, y) => x.Id == y.Id && x.DisplayName == y.DisplayName).All(same => same);
    }
}
