using System;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>The active plugin (with its unsaved marker), the load order and record counts.</summary>
    public sealed class WorldSection : IPanelSection
    {
        const string DirtyClass = "is-dirty";

        readonly EditorContext _context;
        readonly Label _plugin;
        readonly Label _loadOrder;
        readonly Label _counts;

        public WorldSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _plugin = PanelElements.Required<Label>(root, "plugin");
            _loadOrder = PanelElements.Required<Label>(root, "load-order");
            _counts = PanelElements.Required<Label>(root, "record-counts");
        }

        public void Refresh()
        {
            var session = _context.Session;
            var world = session.World;
            PanelElements.SetText(_plugin, session.IsDirty ? $"{session.ActivePluginId} ●" : session.ActivePluginId);
            _plugin.EnableInClassList(DirtyClass, session.IsDirty);
            PanelElements.SetText(_loadOrder, $"Load order: {string.Join(" → ", world.LoadOrder)}");
            PanelElements.SetText(_counts, $"Records: {world.Records.Count}   Conflicts: {world.Conflicts.Count}   Warnings: {world.Warnings.Count}");
        }
    }
}
