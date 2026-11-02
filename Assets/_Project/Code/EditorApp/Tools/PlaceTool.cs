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

            if (!TryGetBase(out var baseObject) || input.PointerOverUI || !TryGetPose(input.Ray, baseObject, out var pose))
            {
                _ghost.Hide();
                return;
            }

            _ghost.Place(pose.Position, pose.Rotation, pose.Scale);
            if (input.PrimaryPressed)
                Place(baseObject, pose);
        }

        bool TryGetBase(out BaseObjectRecord baseObject)
        {
            baseObject = null;
            return !_context.SelectedBaseId.IsDefault
                   && _context.Session.World.Records.TryGet(_context.SelectedBaseId, out var record)
                   && (baseObject = record as BaseObjectRecord) != null;
        }

        // The object's lowest point rests on the surface under the pointer (another object counts,
        // so things can be stacked), turned by the chosen yaw plus the drawn variation and scaled by it.
        bool TryGetPose(Ray ray, BaseObjectRecord baseObject, out PlacementPose pose)
        {
            pose = default;
            if (!_picker.TryPickWorld(ray, out var hit))
                return false;

            _ghost.Use(baseObject.AssetKey);
            var roll = _context.PlacementRoll;
            var rotation = Quaternion.Euler(0f, _context.PlacementYaw + roll.YawDegrees, 0f);
            // The pivot-to-bottom distance grows with the scale; yaw doesn't change it.
            var position = hit.point + Vector3.up * (_ghost.PivotAboveBottom * roll.Scale);
            pose = new PlacementPose(position, rotation, roll.Scale);
            return true;
        }

        void Place(BaseObjectRecord baseObject, in PlacementPose pose)
        {
            var session = _context.Session;
            var id = session.NewId("ref");
            var record = new PlacedObjectRecord(id, baseObject.Id, pose.Position.ToNumerics(), pose.Rotation.ToNumerics(), pose.Scale);

            if (_context.TryEdit("Place", () => session.Put(record, $"Place {baseObject.DisplayName}")))
            {
                _context.Selection.Set(id);
                _context.Report($"Placed {baseObject.DisplayName} ({id})");
                // A fresh variation for the next one, so repeated clicks don't stamp copies.
                _context.RerollPlacement();
            }
        }

        readonly struct PlacementPose
        {
            public PlacementPose(Vector3 position, Quaternion rotation, float scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public Vector3 Position { get; }

            public Quaternion Rotation { get; }

            public float Scale { get; }
        }
    }
}
