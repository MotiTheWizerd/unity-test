using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing;
using Forge.Editing.Geometry;
using Forge.EditorApp.Gizmos;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Click to select (Shift adds/removes), drag a gizmo handle to move the
    /// selection, Del to delete it, Esc to cancel a drag or clear the selection.
    /// A drag previews directly on the scene objects and commits a single
    /// undoable move when released.
    /// </summary>
    public sealed class SelectMoveTool : IEditorTool, IDisposable
    {
        readonly EditorContext _context;
        readonly WorldView _view;
        readonly ScenePicker _picker;
        readonly Camera _camera;
        readonly TranslateGizmo _gizmo;
        Drag _drag;

        public SelectMoveTool(EditorContext context, WorldView view, ScenePicker picker, Camera camera)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _gizmo = new TranslateGizmo(picker.GizmoLayer);
        }

        public void Activate() => _context.Report("Select: click objects, drag the gizmo to move");

        public void Deactivate()
        {
            CancelDrag();
            _gizmo.Hide();
        }

        public void Dispose() => _gizmo.Dispose();

        public void Tick(in ToolInput input)
        {
            if (_drag != null)
            {
                TickDrag(input);
                return;
            }

            var selection = _context.Selection;
            if (input.Delete && !selection.IsEmpty)
            {
                var ids = selection.Ids.ToList();
                _context.TryEdit("Delete", () => ObjectEdits.Delete(_context.Session, ids));
                return;
            }

            if (input.Cancel)
                selection.Clear();

            var pivot = ObjectEdits.Pivot(_context.Session, selection.Ids);
            GizmoHandle? hovered = null;
            if (pivot.HasValue)
            {
                _gizmo.Show(pivot.Value.ToUnity(), _camera);
                if (_picker.TryPickHandle(input.Ray, out var handle))
                    hovered = handle;
            }
            else
            {
                _gizmo.Hide();
            }

            _gizmo.Highlight(hovered);

            if (!input.PrimaryPressed)
                return;

            if (hovered.HasValue && pivot.HasValue)
                BeginDrag(hovered.Value, pivot.Value, input.Ray);
            else
                Pick(input);
        }

        void Pick(in ToolInput input)
        {
            var selection = _context.Selection;
            if (_picker.TryPickPlacedObject(input.Ray, _context.Session.World.Records, out var id))
            {
                if (input.Additive)
                    selection.Toggle(id);
                else
                    selection.Set(id);
            }
            else if (!input.Additive)
            {
                selection.Clear();
            }
        }

        void BeginDrag(GizmoHandle handle, System.Numerics.Vector3 pivot, Ray ray)
        {
            var originals = _context.Selection.Ids
                .Select(id => _context.Session.World.Records.TryGet(id, out var r) ? r as PlacedObjectRecord : null)
                .Where(p => p != null)
                .ToDictionary(p => p.Id, p => p.Position);

            if (originals.Count == 0 || !TryDragParameter(handle, pivot, ray, out var start))
                return;

            _drag = new Drag(handle, pivot, start, originals);
        }

        void TickDrag(in ToolInput input)
        {
            if (input.Cancel)
            {
                CancelDrag();
                _context.Report("Move cancelled");
                return;
            }

            if (TryDragParameter(_drag.Handle, _drag.Pivot, input.Ray, out var current))
                _drag.Delta = DeltaFor(_drag.Handle, _drag.Start, current);

            Preview(_drag.Delta);

            if (input.PrimaryReleased || !input.PrimaryHeld)
                CommitDrag();
        }

        void Preview(System.Numerics.Vector3 delta)
        {
            foreach (var pair in _drag.Originals)
            {
                if (_view.TryGetObject(pair.Key, out var go))
                    go.transform.localPosition = (pair.Value + delta).ToUnity();
            }

            _gizmo.Show((_drag.Pivot + delta).ToUnity(), _camera);
        }

        void CommitDrag()
        {
            var drag = _drag;
            _drag = null;
            if (drag.Delta == System.Numerics.Vector3.Zero)
                return;

            var ids = drag.Originals.Keys.ToList();
            if (!_context.TryEdit("Move", () => ObjectEdits.Move(_context.Session, ids, drag.Delta)))
                _view.ResetTransforms(ids);
            else
                _context.Report($"Moved {ids.Count} object(s)");
        }

        void CancelDrag()
        {
            if (_drag == null)
                return;
            _view.ResetTransforms(_drag.Originals.Keys);
            _drag = null;
        }

        // Axis handles track the distance along the axis; the plane handle tracks a point on the horizontal plane.
        static bool TryDragParameter(GizmoHandle handle, System.Numerics.Vector3 pivot, Ray ray, out System.Numerics.Vector3 value)
        {
            var origin = ray.origin.ToNumerics();
            var direction = ray.direction.ToNumerics();
            value = default;

            if (handle == GizmoHandle.PlaneXZ)
                return DragMath.TryIntersectPlane(origin, direction, pivot, System.Numerics.Vector3.UnitY, out value);

            var axis = TranslateGizmo.AxisDirection(handle).ToNumerics();
            if (!DragMath.TryProjectOnAxis(origin, direction, pivot, axis, out var t))
                return false;

            value = axis * t;
            return true;
        }

        static System.Numerics.Vector3 DeltaFor(GizmoHandle handle, System.Numerics.Vector3 start, System.Numerics.Vector3 current)
        {
            var delta = current - start;
            return handle == GizmoHandle.PlaneXZ ? new System.Numerics.Vector3(delta.X, 0f, delta.Z) : delta;
        }

        sealed class Drag
        {
            public Drag(GizmoHandle handle, System.Numerics.Vector3 pivot, System.Numerics.Vector3 start, Dictionary<RecordId, System.Numerics.Vector3> originals)
            {
                Handle = handle;
                Pivot = pivot;
                Start = start;
                Originals = originals;
            }

            public GizmoHandle Handle { get; }

            public System.Numerics.Vector3 Pivot { get; }

            public System.Numerics.Vector3 Start { get; }

            public Dictionary<RecordId, System.Numerics.Vector3> Originals { get; }

            public System.Numerics.Vector3 Delta { get; set; }
        }
    }
}
