using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using Forge.Editing;
using Forge.Editing.Placement;
using Forge.Editing.Water;
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

        /// <summary>
        /// Repaints the whole space on show by its shape (the Paint tool's Auto button): rock on steep ground, dirt on
        /// slopes, sand at the shore, unpainted ground's layer elsewhere. One undo step.
        /// </summary>
        public void AutoPaintSpace()
        {
            var records = _context.Session.World.Records;
            if (!(_context.UnpaintedGround is RecordId flat))
            {
                _context.Report("Auto paint: no ground layer looks like unpainted ground");
                return;
            }

            // SHORTCUT: the roles are the base game's looks; per-world rules (a climate's or worldspace's) become data
            // when worlds need their own ground (see ARCHITECTURE.md).
            RecordId? Look(string assetKey) => Forge.Editing.Terrain.GroundLayers.WithLook(records, assetKey);
            var rules = new Forge.Editing.Terrain.AutoPaintRules(flat, Look("ground/dirt"), Look("ground/rock"), Look("ground/sand"));

            var painted = 0;
            if (_context.TryEdit("Auto paint", () => painted = Forge.Editing.Terrain.AutoPaint.Apply(_context.Session, _context.ActiveSpace, rules)))
                _context.Report(painted > 0 ? $"Auto painted {painted} chunk(s) by their shape" : "Auto paint: this space has no land");
        }

        /// <summary>Moves the current lake's surface to <paramref name="level"/> (the Water tool's level field).</summary>
        public void SetCurrentLakeLevel(float level)
        {
            var lake = _context.CurrentLakeRecord;
            if (lake == null)
                return;
            var changed = false;
            if (_context.TryEdit("Lake level", () => changed = WaterEdits.SetLakeLevel(_context.Session, lake.Id, level)) && changed)
                _context.Report(_context.Session.UndoDescription);
        }

        /// <summary>Removes the current lake (Del in the Water tool, or its Remove button).</summary>
        public void RemoveCurrentLake()
        {
            var lake = _context.CurrentLakeRecord;
            if (lake == null)
            {
                _context.Report("Water: click a lake to pick it first");
                return;
            }

            if (_context.TryEdit("Remove lake", () => WaterEdits.RemoveLake(_context.Session, lake.Id)))
            {
                _context.CurrentLake = null;
                _context.Report("Removed the lake");
            }
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
        // The object's local position is its world one (it sits under the world root); heights are never rebased, so
        // the renderer bounds' heights are world heights too.
        bool TryDropStraight(PlacedObjectRecord placed, out (System.Numerics.Vector3, System.Numerics.Quaternion) pose)
        {
            pose = default;
            if (!_view.TryGetObject(placed.Id, out var go) || !RendererBounds.TryGet(go, out var bounds))
                return false;

            var position = go.transform.localPosition;
            var probe = new Ray(new Vector3(position.x, bounds.max.y + ProbeHeight, position.z), Vector3.down);
            if (!_picker.TryPickTerrain(probe, out var ground))
                return false;

            var target = placed.Position;
            target.Y = ground.Point.y + (position.y - bounds.min.y);
            pose = (target, placed.Rotation);
            return true;
        }
    }
}
