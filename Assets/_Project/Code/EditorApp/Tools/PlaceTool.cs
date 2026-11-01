using System;
using Forge.Data.Records;
using Forge.World;

namespace Forge.EditorApp.Tools
{
    /// <summary>Click a surface to place the palette's base object there; the new object becomes the selection.</summary>
    public sealed class PlaceTool : IEditorTool
    {
        readonly EditorContext _context;
        readonly ScenePicker _picker;

        public PlaceTool(EditorContext context, ScenePicker picker)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        }

        public void Activate() => _context.Report($"Place: {_context.DisplayNameOf(_context.SelectedBaseId)} (Esc to stop)");

        public void Deactivate()
        {
        }

        public void Tick(in ToolInput input)
        {
            if (input.Cancel)
            {
                _context.ActiveTool = ToolKind.Select;
                return;
            }

            if (!input.PrimaryPressed || _context.SelectedBaseId.IsDefault || !_picker.TryPickWorld(input.Ray, out var hit))
                return;

            var session = _context.Session;
            var id = session.NewId("ref");
            var name = _context.DisplayNameOf(_context.SelectedBaseId);
            var record = new PlacedObjectRecord(id, _context.SelectedBaseId, hit.point.ToNumerics(), System.Numerics.Quaternion.Identity, 1f);

            if (_context.TryEdit("Place", () => session.Put(record, $"Place {name}")))
            {
                _context.Selection.Set(id);
                _context.Report($"Placed {name} ({id})");
            }
        }
    }
}
