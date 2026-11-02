using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Editing;
using Forge.Editing.Placement;
using Forge.EditorApp.Gizmos;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Shows a see-through copy of the palette's base object resting on the surface under the
    /// pointer, turned by <see cref="EditorContext.PlacementYaw"/> ([ / ]) plus its drawn variation,
    /// and tilted to the slope when its rules ask for it (<see cref="ObjectSeating"/>).
    /// A click places it exactly there. Holding and dragging paints a row (<see cref="PlacementStroke"/>):
    /// a new object each time the pointer is clear of the last one plus the gap, Shift for a straight
    /// line. The row previews as ghosts and is placed as one undo step on release; Esc drops it.
    /// Preview and records come from the same computed poses, so what is shown is what gets placed.
    /// </summary>
    public sealed class PlaceTool : IEditorTool, IDisposable
    {
        readonly EditorContext _context;
        readonly ScenePicker _picker;
        readonly PlacementGhost _ghost;
        readonly ObjectSeating _seating;
        readonly List<PlacementPose> _poses = new List<PlacementPose>();
        PlacementStroke _stroke;
        PlacementPose _pressPose;
        bool _pressOnTerrain;

        public PlaceTool(EditorContext context, ScenePicker picker, PlacementGhost ghost, ObjectSeating seating)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _ghost = ghost ?? throw new ArgumentNullException(nameof(ghost));
            _seating = seating ?? throw new ArgumentNullException(nameof(seating));
        }

        public void Activate() =>
            _context.Report($"Place: {_context.DisplayNameOf(_context.SelectedBaseId)} (drag to paint a row, Shift: straight, [ ] rotate, Esc to stop)");

        public void Deactivate()
        {
            _stroke = null;
            _ghost.Hide();
        }

        public void Dispose() => _ghost.Dispose();

        public void Tick(in ToolInput input)
        {
            if (input.Cancel)
            {
                if (_stroke != null)
                {
                    _stroke = null;
                    _ghost.Hide();
                    _context.Report("Placement cancelled");
                }
                else
                {
                    _context.ActiveTool = ToolKind.Select;
                }

                return;
            }

            if (!TryGetBase(out var baseObject))
            {
                Deactivate();
                return;
            }

            if (_stroke != null)
            {
                ContinueStroke(input, baseObject);
                return;
            }

            if (input.PointerOverUI || !_picker.TryPickWorld(input.Ray, out var hit))
            {
                _ghost.Hide();
                return;
            }

            _ghost.Use(baseObject.AssetKey);
            var roll = _context.PlacementRoll;
            var pose = PoseAt(baseObject, hit.point, hit.collider is TerrainCollider, _context.PlacementYaw + roll.YawDegrees, roll.Scale);
            _poses.Clear();
            _poses.Add(pose);
            ShowGhosts(_poses);

            if (input.PrimaryPressed)
                BeginStroke(pose, hit.collider is TerrainCollider);
        }

        bool TryGetBase(out BaseObjectRecord baseObject)
        {
            baseObject = null;
            return !_context.SelectedBaseId.IsDefault
                   && _context.Session.World.Records.TryGet(_context.SelectedBaseId, out var record)
                   && (baseObject = record as BaseObjectRecord) != null;
        }

        void BeginStroke(PlacementPose pressPose, bool onTerrain)
        {
            _pressPose = pressPose;
            _pressOnTerrain = onTerrain;
            _stroke = new PlacementStroke(
                Flat(pressPose.Ground),
                _ghost.Shape.Footprint,
                alignToPath: !_context.PlacementHasRandomYaw,
                yawOffset: _context.PlacementYaw,
                firstRoll: _context.PlacementRoll,
                drawRoll: _context.DrawPlacementRoll,
                drawGap: _context.DrawPlacementGap);
        }

        void ContinueStroke(in ToolInput input, BaseObjectRecord baseObject)
        {
            if (input.PrimaryHeld && !input.PointerOverUI && _picker.TryPickWorld(input.Ray, out var hit))
                _stroke.MoveTo(Flat(hit.point), line: input.Additive);

            BuildPoses(baseObject);
            ShowGhosts(_poses);

            if (input.PrimaryReleased || !input.PrimaryHeld)
                CommitStroke(baseObject);
        }

        // The first object keeps the exact spot that was clicked (so a click can still stack on another
        // object); the rest of a row rests on the terrain under each one.
        void BuildPoses(BaseObjectRecord baseObject)
        {
            _poses.Clear();
            var items = _stroke.Items;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (i == 0)
                {
                    _poses.Add(PoseAt(baseObject, _pressPose.Ground, _pressOnTerrain, item.YawDegrees, item.Scale));
                    continue;
                }

                var height = _picker.TerrainHeightAt(item.Position);
                var ground = new Vector3(item.Position.X, height ?? _pressPose.Ground.y, item.Position.Y);
                _poses.Add(PoseAt(baseObject, ground, height.HasValue, item.YawDegrees, item.Scale));
            }
        }

        // The object rests on `ground`, seated the way the world-building commands seat it too.
        PlacementPose PoseAt(BaseObjectRecord baseObject, Vector3 ground, bool onTerrain, float yawDegrees, float scale)
        {
            var pose = _seating.Seat(baseObject, ground.ToNumerics(), onTerrain, yawDegrees, scale);
            return new PlacementPose(ground, pose.Position.ToUnity(), pose.Rotation.ToUnity(), scale);
        }

        void ShowGhosts(IReadOnlyList<PlacementPose> poses)
        {
            for (var i = 0; i < poses.Count; i++)
                _ghost.Show(i, poses[i].Position, poses[i].Rotation, poses[i].Scale);
            _ghost.HideFrom(poses.Count);
        }

        void CommitStroke(BaseObjectRecord baseObject)
        {
            _stroke = null;
            _ghost.Hide();
            if (_poses.Count == 0)
                return;

            var placements = _poses
                .Select(p => new ObjectPlacement(baseObject.Id, new ObjectPose(p.Position.ToNumerics(), p.Rotation.ToNumerics()), p.Scale))
                .ToList();
            var name = baseObject.DisplayName;
            var description = placements.Count == 1 ? $"Place {name}" : $"Place {placements.Count} {name}";
            IReadOnlyList<PlacedObjectRecord> records = null;
            if (_context.TryEdit("Place", () => records = ObjectEdits.Place(_context.Session, placements, description)))
            {
                _context.Selection.Replace(records.Select(r => r.Id));
                _context.Report(records.Count == 1 ? $"Placed {name} ({records[0].Id})" : $"Placed {records.Count} × {name}");
                // A fresh variation for the next one, so repeated clicks don't stamp copies.
                _context.RerollPlacement();
            }
        }

        static System.Numerics.Vector2 Flat(Vector3 point) => new System.Numerics.Vector2(point.x, point.z);

        readonly struct PlacementPose
        {
            public PlacementPose(Vector3 ground, Vector3 position, Quaternion rotation, float scale)
            {
                Ground = ground;
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            /// <summary>The surface point the object rests on.</summary>
            public Vector3 Ground { get; }

            /// <summary>Where the pivot goes.</summary>
            public Vector3 Position { get; }

            public Quaternion Rotation { get; }

            public float Scale { get; }
        }
    }
}
