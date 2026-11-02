using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using Forge.Editing;
using Forge.Editing.Placement;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// One-shot editor actions shared by keyboard shortcuts and UI buttons, so
    /// each action has exactly one implementation.
    /// </summary>
    public sealed class EditorCommands
    {
        // Rays start this far above an object's top, so they begin above any terrain under it.
        const float ProbeHeight = 1000f;

        readonly EditorContext _context;
        readonly WorldView _view;
        readonly ScenePicker _picker;
        readonly ICameraRig _camera;
        readonly SlopeAlignment _slopes;

        public EditorCommands(EditorContext context, WorldView view, ScenePicker picker, ICameraRig camera, SlopeAlignment slopes)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _slopes = slopes ?? throw new ArgumentNullException(nameof(slopes));
        }

        /// <summary>Glides the camera so the whole selection fills the view (F).</summary>
        public void FrameSelection()
        {
            var found = false;
            var combined = default(Bounds);
            foreach (var id in _context.Selection.Ids)
            {
                if (!_view.TryGetObject(id, out var go) || !RendererBounds.TryGet(go, out var bounds))
                    continue;
                if (found)
                    combined.Encapsulate(bounds);
                else
                    combined = bounds;
                found = true;
            }

            if (!found)
            {
                _context.Report("Frame: nothing selected");
                return;
            }

            _camera.Frame(combined.center, combined.extents.magnitude);
        }

        /// <summary>
        /// Rests each selected object's lowest point on the terrain directly below
        /// (or above, if it is buried). Objects that align to the slope are seated and tilted
        /// to the ground under their footprint, exactly as placing does. One undo step for the
        /// whole selection.
        /// </summary>
        public void DropSelectionToGround()
        {
            if (_context.Selection.IsEmpty)
            {
                _context.Report("Drop to ground: nothing selected");
                return;
            }

            var poses = new Dictionary<RecordId, (System.Numerics.Vector3 Position, System.Numerics.Quaternion Rotation)>();
            var missed = 0;
            foreach (var id in _context.Selection.Ids)
            {
                if (!_context.Session.World.Records.TryGet(id, out var record) || !(record is PlacedObjectRecord placed))
                    continue;

                if (TrySeatOnSlope(placed, out var pose) || TryDropStraight(placed, out pose))
                    poses[id] = pose;
                else
                    missed++;
            }

            var moved = 0;
            if (_context.TryEdit("Drop to ground", () => moved = ObjectEdits.Transform(_context.Session, poses.Keys,
                    placed => placed.WithTransform(poses[placed.Id].Position, poses[placed.Id].Rotation, placed.Scale), "Drop")))
            {
                _context.Report(missed == 0
                    ? $"Dropped {moved} object(s) to ground"
                    : $"Dropped {moved} object(s); {missed} had no terrain below");
            }
        }

        // Fences, walls, rocks: the same seating as placing, keeping the object's heading.
        bool TrySeatOnSlope(PlacedObjectRecord placed, out (System.Numerics.Vector3, System.Numerics.Quaternion) pose)
        {
            pose = default;
            var shape = _slopes.ShapeToAlign(placed);
            if (!shape.HasValue)
                return false;

            var center = new System.Numerics.Vector2(placed.Position.X, placed.Position.Z);
            if (!GroundFit.TryPose(center, shape.Value, PlacementMath.HeadingOf(placed.Rotation), placed.Scale,
                    _picker.TerrainHeightAt, GroundFit.DefaultMaxTiltDegrees, out var pivot, out var rotation))
                return false;

            pose = (pivot, rotation);
            return true;
        }

        // Everything else drops straight down, keeping its rotation, so its lowest drawn point lands on the surface.
        bool TryDropStraight(PlacedObjectRecord placed, out (System.Numerics.Vector3, System.Numerics.Quaternion) pose)
        {
            pose = default;
            if (!_view.TryGetObject(placed.Id, out var go) || !RendererBounds.TryGet(go, out var bounds))
                return false;

            var probe = new Ray(new Vector3(go.transform.position.x, bounds.max.y + ProbeHeight, go.transform.position.z), Vector3.down);
            if (!_picker.TryPickTerrain(probe, out var ground))
                return false;

            var target = placed.Position;
            target.Y = ground.point.y + (go.transform.position.y - bounds.min.y);
            pose = (target, placed.Rotation);
            return true;
        }
    }
}
