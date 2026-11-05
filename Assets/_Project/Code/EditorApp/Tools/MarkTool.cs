using System;
using Forge.Editing.Geometry;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Marks where the user means when they ask the agent for "here": click the ground for a spot, or drag for an
    /// area. The mark stays (and shows) until it is replaced or cleared with Del; Esc cancels a drag, or returns
    /// to the Select tool. Marking never edits the world, so it works while the agent's build is open too.
    /// </summary>
    public sealed class MarkTool : IEditorTool
    {
        readonly EditorContext _context;
        readonly ScenePicker _picker;
        System.Numerics.Vector2? _dragStart;
        GroundMark _beforeDrag;

        public MarkTool(EditorContext context, ScenePicker picker)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        }

        // The controller shows the tool's hint (ToolCatalog).
        public void Activate()
        {
        }

        public void Deactivate() => CancelDrag();

        public void Tick(in ToolInput input)
        {
            if (input.Cancel)
            {
                if (_dragStart.HasValue)
                    CancelDrag();
                else
                    _context.SelectTool(ToolKind.Select);
                return;
            }

            if (!_dragStart.HasValue)
            {
                if (input.Delete && _context.Mark != null)
                {
                    _context.Mark = null;
                    _context.Report("Mark cleared");
                }
                else if (input.PrimaryPressed && _picker.TryPickTerrain(input.Ray, out var start))
                {
                    _beforeDrag = _context.Mark;
                    _dragStart = Flat(start.Point);
                    _context.Mark = GroundMark.Spot(_dragStart.Value);
                }

                return;
            }

            // The mark follows the drag live, so it shows what letting go will mark.
            if (!input.PointerOverUI && _picker.TryPickTerrain(input.Ray, out var hit))
                _context.Mark = GroundMark.FromDrag(_dragStart.Value, Flat(hit.Point));

            if (input.PrimaryReleased || !input.PrimaryHeld)
            {
                _dragStart = null;
                _beforeDrag = null;
                _context.Report($"Marked {_context.Mark.Describe()}");
            }
        }

        void CancelDrag()
        {
            if (!_dragStart.HasValue)
                return;
            _context.Mark = _beforeDrag;
            _dragStart = null;
            _beforeDrag = null;
        }

        static System.Numerics.Vector2 Flat(Vector3 point) => new System.Numerics.Vector2(point.x, point.z);
    }
}
