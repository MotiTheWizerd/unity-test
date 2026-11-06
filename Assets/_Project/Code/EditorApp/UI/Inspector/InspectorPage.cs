using System;
using System.Collections.Generic;
using System.Linq;
using Forge.EditorApp.UI.Controls;
using Forge.Editing.Inspection;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Inspector
{
    /// <summary>
    /// The side dock's Inspector page: the selection's details as a stack of sections (<see cref="IInspectorSection"/>),
    /// each in a collapsible <see cref="ForgeSection"/>, in the order given. What is inspected is read once into an
    /// <see cref="Inspection"/> when the selection or the world changes, and only while the page is on show: a hidden
    /// page costs nothing, and showing it catches up.
    /// </summary>
    public sealed class InspectorPage : IPanelSection
    {
        const string EmptyHint = "Select something to inspect it.";

        readonly EditorContext _context;
        readonly ForgeSideDock _dock;
        readonly Label _empty;
        readonly List<(IInspectorSection Section, ForgeSection Frame)> _sections = new List<(IInspectorSection, ForgeSection)>();
        object _world;
        bool _selectionChanged = true;

        public InspectorPage(VisualElement root, ForgeSideDock dock, EditorContext context, IEnumerable<IInspectorSection> sections)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dock = dock ?? throw new ArgumentNullException(nameof(dock));
            if (sections == null)
                throw new ArgumentNullException(nameof(sections));

            _empty = PanelElements.Required<Label>(root, "inspector-empty");
            PanelElements.SetText(_empty, EmptyHint);
            var host = PanelElements.Required<VisualElement>(root, "inspector-sections");
            foreach (var section in sections)
            {
                var frame = new ForgeSection { title = section.Title, collapsible = true };
                frame.Add(section.Content);
                host.Add(frame);
                _sections.Add((section, frame));
            }

            _context.Selection.Changed += () => _selectionChanged = true;
        }

        public void Refresh()
        {
            if (!_dock.IsOpen || _dock.Current != SideDockSection.InspectorPage)
                return;

            // Every edit publishes a new world, so a new one means the records may have changed.
            var world = _context.Session.World;
            if (_selectionChanged || !ReferenceEquals(world, _world))
            {
                _selectionChanged = false;
                _world = world;
                Show(Inspection.Of(world.Records, _context.Selection.Ids));
            }

            foreach (var (section, frame) in _sections)
            {
                if (PanelElements.IsShown(frame))
                    section.Refresh();
            }
        }

        void Show(Inspection inspection)
        {
            PanelElements.SetVisible(_empty, inspection.IsEmpty);
            foreach (var (section, frame) in _sections)
            {
                var shows = !inspection.IsEmpty && section.Shows(inspection);
                PanelElements.SetVisible(frame, shows);
                if (shows)
                    section.Show(inspection);
            }
        }

        /// <summary>The sections, in order (for tests and tools).</summary>
        public IEnumerable<IInspectorSection> Sections => _sections.Select(s => s.Section);
    }
}
