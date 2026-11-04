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
    /// Drag a rectangle on the ground from corner to corner to lay the palette's object around it (walls, fences)
    /// or over it (floor tiles), by <see cref="EditorContext.OutlinePattern"/>, with the same layout as the
    /// <c>place_outline</c> command (<see cref="RectLayout"/>). The rectangle snaps to whole pieces unless that is
    /// switched off, Shift makes it square, and it is turned by the placement turn ([ ]). The pieces preview as
    /// ghosts, seated on the ground exactly as placing seats them, and are placed as one undo step on release;
    /// Esc drops the rectangle, or returns to the Select tool.
    /// </summary>
    public sealed class OutlineTool : IEditorTool, IDisposable
    {
        // A press only becomes a rectangle once dragged this far, so a stray click places nothing.
        const float MinDragDistance = 0.5f;

        readonly EditorContext _context;
        readonly ScenePicker _picker;
        readonly PlacementGhost _ghost;
        readonly ObjectSeating _seating;
        readonly List<PlacementRoll> _rolls = new List<PlacementRoll>();
        readonly List<ObjectPlacement> _placements = new List<ObjectPlacement>();
        System.Numerics.Vector2? _start;

        public OutlineTool(EditorContext context, ScenePicker picker, PlacementGhost ghost, ObjectSeating seating)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _ghost = ghost ?? throw new ArgumentNullException(nameof(ghost));
            _seating = seating ?? throw new ArgumentNullException(nameof(seating));
        }

        // The controller shows the tool's hint (ToolCatalog).
        public void Activate()
        {
        }

        public void Deactivate() => DropRectangle();

        public void Dispose() => _ghost.Dispose();

        public void Tick(in ToolInput input)
        {
            if (input.Cancel)
            {
                if (_start.HasValue)
                {
                    DropRectangle();
                    _context.Report("Outline cancelled");
                }
                else
                {
                    _context.SelectTool(ToolKind.Select);
                }

                return;
            }

            var baseObject = _context.SelectedBaseObject;
            if (baseObject == null)
            {
                DropRectangle();
                return;
            }

            if (!_start.HasValue)
            {
                if (input.PrimaryPressed && !input.PointerOverUI && _picker.TryPickTerrain(input.Ray, out var press))
                {
                    _start = Flat(press.point);
                    _rolls.Clear();
                }

                return;
            }

            if (input.PrimaryHeld && !input.PointerOverUI && _picker.TryPickTerrain(input.Ray, out var hit))
                LayOut(baseObject, Flat(hit.point), square: input.Additive);

            if (input.PrimaryReleased || !input.PrimaryHeld)
                Commit(baseObject);
        }

        // The rectangle from the press to the pointer, its pieces seated on the ground and shown as ghosts.
        void LayOut(BaseObjectRecord baseObject, System.Numerics.Vector2 pointer, bool square)
        {
            _placements.Clear();
            if (System.Numerics.Vector2.Distance(pointer, _start.Value) < MinDragDistance)
            {
                _ghost.Hide();
                return;
            }

            var pattern = _context.OutlinePattern;
            var footprint = _seating.ShapeOf(baseObject).Footprint;
            var gap = _context.PlacementGap;
            var rect = RectLayout.FromDrag(_start.Value, pointer, _context.PlacementYaw, square, pattern, footprint, gap, _context.OutlineSnapToPieces);
            var size = FormattableString.Invariant($"{rect.Width:0.#} × {rect.Depth:0.#} m");

            var count = RectLayout.Count(rect, pattern, footprint, gap);
            if (count > RectLayout.MaxItems)
            {
                _ghost.Hide();
                _context.Report($"{size} needs {count} pieces; at most {RectLayout.MaxItems}. Drag a smaller rectangle.");
                return;
            }

            var items = RectLayout.Lay(rect, pattern, footprint, gap, alignToRect: !_context.PlacementHasRandomYaw, RollAt);
            _ghost.Use(baseObject.AssetKey);
            foreach (var item in items)
            {
                var pose = _seating.SeatOnTerrain(baseObject, item.Position, item.YawDegrees, item.Scale);
                if (!pose.HasValue)
                    continue;
                _ghost.Show(_placements.Count, pose.Value.Position.ToUnity(), pose.Value.Rotation.ToUnity(), item.Scale);
                _placements.Add(new ObjectPlacement(baseObject.Id, pose.Value, item.Scale));
            }

            _ghost.HideFrom(_placements.Count);
            var skipped = items.Count - _placements.Count;
            _context.Report($"{size} · {_placements.Count} {baseObject.DisplayName}" + (skipped > 0 ? $" ({skipped} off the ground)" : ""));
        }

        // Each piece keeps the variation it was first drawn with, so re-laying the rectangle doesn't flicker.
        PlacementRoll RollAt(int index)
        {
            while (_rolls.Count <= index)
                _rolls.Add(_context.DrawPlacementRoll());
            return _rolls[index];
        }

        void Commit(BaseObjectRecord baseObject)
        {
            var placements = _placements.ToList();
            var pattern = _context.OutlinePattern;
            DropRectangle();
            if (placements.Count == 0)
                return;

            var description = $"Place {placements.Count} {baseObject.DisplayName} {RectLayout.Preposition(pattern)} a rectangle";
            IReadOnlyList<PlacedObjectRecord> records = null;
            if (_context.TryEdit("Outline", () => records = ObjectEdits.Place(_context.Session, _context.ActiveSpace, placements, description)))
            {
                _context.Selection.Replace(records.Select(r => r.Id));
                _context.Report($"Placed {records.Count} × {baseObject.DisplayName}");
                _context.RerollPlacement();
            }
        }

        void DropRectangle()
        {
            _start = null;
            _placements.Clear();
            _ghost.Hide();
        }

        static System.Numerics.Vector2 Flat(Vector3 point) => new System.Numerics.Vector2(point.x, point.z);
    }
}
