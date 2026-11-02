using System;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>The bottom status line: the editor's last status message.</summary>
    public sealed class StatusBar : IPanelSection
    {
        readonly EditorContext _context;
        readonly Label _status;

        public StatusBar(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _status = PanelElements.Required<Label>(root, "status");
        }

        public void Refresh() => PanelElements.SetText(_status, _context.Status ?? string.Empty);
    }
}
