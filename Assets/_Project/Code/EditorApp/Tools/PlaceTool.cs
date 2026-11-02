using System;
using Forge.Data.Records;
using Forge.EditorApp.Gizmos;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Shows a see-through copy of the palette's base object resting on the surface under the
    /// pointer, turned by <see cref="EditorContext.PlacementYaw"/> ([ / ]); a click places it
    /// exactly there and selects it. The preview and the placed record come from the same
    /// computed pose, so what is shown is what gets placed.
    /// </summary>
    public sealed class PlaceTool : IEditorTool, IDisposable
    {
        readonly EditorContext _context;
        readonly ScenePicker _picker;
        readonly PlacementGhost _ghost;

        public PlaceTool(EditorContext context, ScenePicker picker, PlacementGhost ghost)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _ghost = ghost ?? throw new ArgumentNullException(nameof(ghost));
        }

        public void Activate() => _context.Report($"Place: {_context.DisplayNameOf(_context.SelectedBaseId)} ([ ] rotate, Esc to stop)");

        public void Deactivate() => _ghost.Hide();

        public void Dispose() => _ghost.Dispose();

        public void Tick(in ToolInput input)
        {
            if (input.Cancel)
            {
                _context.ActiveTool = ToolKind.Select;
                return;
            }

            if (!TryGetBase(out var baseObject) || input.PointerOverUI || !TryGetPose(input.Ray, baseObject, out var position, out var rotation))
            {
                _ghost.Hide();
                return;
            }

            _ghost.Place(position, rotation);
            if (input.PrimaryPressed)
                Place(baseObject, position, rotation);
        }

        bool TryGetBase(out BaseObjectRecord baseObject)
        {
            baseObject = null;
            return !_context.SelectedBaseId.IsDefault
                   && _context.Session.World.Records.TryGet(_context.SelectedBaseId, out var record)
                   && (baseObject = record as BaseObjectRecord) != null;
        }

        // The object's lowest point rests on the surface under the pointer (another object counts,
        // so things can be stacked), turned around the vertical axis.
        bool TryGetPose(Ray ray, BaseObjectRecord baseObject, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.Euler(0f, _context.PlacementYaw, 0f);
            if (!_picker.TryPickWorld(ray, out var hit))
                return false;

            _ghost.Use(baseObject.AssetKey);
            position = hit.point + Vector3.up * _ghost.PivotAboveBottom;
            return true;
        }

        void Place(BaseObjectRecord baseObject, Vector3 position, Quaternion rotation)
        {
            var session = _context.Session;
            var id = session.NewId("ref");
            var record = new PlacedObjectRecord(id, baseObject.Id, position.ToNumerics(), rotation.ToNumerics(), 1f);

            if (_context.TryEdit("Place", () => session.Put(record, $"Place {baseObject.DisplayName}")))
            {
                _context.Selection.Set(id);
                _context.Report($"Placed {baseObject.DisplayName} ({id})");
            }
        }
    }
}
