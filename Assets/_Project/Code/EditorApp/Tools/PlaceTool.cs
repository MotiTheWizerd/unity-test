using System;
using System.Collections.Generic;
using Forge.Data.Records;
using Forge.Editing.Placement;
using Forge.EditorApp.Gizmos;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Shows a see-through copy of the palette's base object resting on the surface under the
    /// pointer, turned by <see cref="EditorContext.PlacementYaw"/> ([ / ]) plus its drawn variation.
    /// A click places it exactly there. Holding and dragging paints a row (<see cref="PlacementStroke"/>):
    /// a new object each time the pointer is clear of the last one plus the gap, Shift for a straight
    /// line. The row previews as ghosts and is placed as one undo step on release; Esc drops it.
    /// Preview and records come from the same computed poses, so what is shown is what gets placed.
    /// </summary>
    public sealed class PlaceTool : IEditorTool, IDisposable
    {
        // Ground probes start this far above an object, so they begin above any terrain under it.
        const float ProbeHeight = 1000f;

        readonly EditorContext _context;
        readonly ScenePicker _picker;
        readonly PlacementGhost _ghost;
        readonly List<PlacementPose> _poses = new List<PlacementPose>();
        PlacementStroke _stroke;
        PlacementPose _pressPose;

        public PlaceTool(EditorContext context, ScenePicker picker, PlacementGhost ghost)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _ghost = ghost ?? throw new ArgumentNullException(nameof(ghost));
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
            var pose = PoseAt(hit.point, _context.PlacementYaw + roll.YawDegrees, roll.Scale);
            _poses.Clear();
            _poses.Add(pose);
            ShowGhosts(_poses);

            if (input.PrimaryPressed)
                BeginStroke(pose);
        }

        bool TryGetBase(out BaseObjectRecord baseObject)
        {
            baseObject = null;
            return !_context.SelectedBaseId.IsDefault
                   && _context.Session.World.Records.TryGet(_context.SelectedBaseId, out var record)
                   && (baseObject = record as BaseObjectRecord) != null;
        }

        void BeginStroke(PlacementPose pressPose)
        {
            _pressPose = pressPose;
            _stroke = new PlacementStroke(
                Flat(pressPose.Ground),
                _ghost.Footprint,
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

            BuildPoses();
            ShowGhosts(_poses);

            if (input.PrimaryReleased || !input.PrimaryHeld)
                CommitStroke(baseObject);
        }

        // The first object keeps the exact spot that was clicked (so a click can still stack on another
        // object); the rest of a row rests on the terrain under each one.
        void BuildPoses()
        {
            _poses.Clear();
            var items = _stroke.Items;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var ground = i == 0 ? _pressPose.Ground : GroundAt(item.Position, _pressPose.Ground.y);
                _poses.Add(PoseAt(ground, item.YawDegrees, item.Scale));
            }
        }

        Vector3 GroundAt(System.Numerics.Vector2 position, float fallbackHeight)
        {
            var probe = new Ray(new Vector3(position.X, fallbackHeight + ProbeHeight, position.Y), Vector3.down);
            return _picker.TryPickTerrain(probe, out var hit)
                ? hit.point
                : new Vector3(position.X, fallbackHeight, position.Y);
        }

        // The object's lowest point rests on `ground`; the pivot-to-bottom distance grows with the scale.
        PlacementPose PoseAt(Vector3 ground, float yawDegrees, float scale) =>
            new PlacementPose(ground, ground + Vector3.up * (_ghost.PivotAboveBottom * scale), Quaternion.Euler(0f, yawDegrees, 0f), scale);

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

            var session = _context.Session;
            var ids = session.NewIds("ref", _poses.Count);
            var records = new List<PlacedObjectRecord>(_poses.Count);
            for (var i = 0; i < _poses.Count; i++)
            {
                var pose = _poses[i];
                records.Add(new PlacedObjectRecord(ids[i], baseObject.Id, pose.Position.ToNumerics(), pose.Rotation.ToNumerics(), pose.Scale));
            }

            var name = baseObject.DisplayName;
            var description = records.Count == 1 ? $"Place {name}" : $"Place {records.Count} {name}";
            if (_context.TryEdit("Place", () => session.Batch(description, () =>
                {
                    foreach (var record in records)
                        session.Put(record);
                })))
            {
                _context.Selection.Replace(ids);
                _context.Report(records.Count == 1 ? $"Placed {name} ({ids[0]})" : $"Placed {records.Count} × {name}");
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
