using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing;
using Forge.Editing.Geometry;
using Forge.Editing.Transforms;
using Forge.EditorApp.Gizmos;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Click to select (Shift adds/removes), then drag the gizmo to move, rotate or
    /// scale the selection (<see cref="EditorContext.TransformMode"/>, W / E / R; hold
    /// Ctrl to snap). Del deletes the selection. Esc cancels a drag or clears the
    /// selection. A drag previews on the scene objects and commits one undoable edit on release.
    /// </summary>
    public sealed class SelectTransformTool : IEditorTool, IDisposable
    {
        readonly EditorContext _context;
        readonly WorldView _view;
        readonly ScenePicker _picker;
        readonly Camera _camera;
        readonly Dictionary<TransformMode, Gizmo> _gizmos;
        ActiveDrag _drag;

        /// <param name="gizmoMaterial">Material for the gizmos (the always-on-top overlay); null for the default.</param>
        public SelectTransformTool(EditorContext context, WorldView view, ScenePicker picker, Camera camera, Material gizmoMaterial)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _gizmos = new Dictionary<TransformMode, Gizmo>
            {
                { TransformMode.Move, GizmoShapes.Translate(picker.GizmoLayer, gizmoMaterial) },
                { TransformMode.Rotate, GizmoShapes.Rotate(picker.GizmoLayer, gizmoMaterial) },
                { TransformMode.Scale, GizmoShapes.Scale(picker.GizmoLayer, gizmoMaterial) },
            };
        }

        public void Activate() => _context.Report("Select: click objects; W move, E rotate, R scale; Ctrl snaps");

        public void Deactivate()
        {
            CancelDrag();
            HideAll();
        }

        public void Dispose()
        {
            foreach (var gizmo in _gizmos.Values)
                gizmo.Dispose();
        }

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

            var gizmo = ShowOnly(_context.TransformMode);
            var pivot = ObjectEdits.Pivot(_context.Session, selection.Ids);
            GizmoHandle? hovered = null;
            if (pivot.HasValue)
            {
                gizmo.Show(pivot.Value.ToUnity(), _camera);
                if (_picker.TryPickHandle(input.Ray, out var handle))
                    hovered = handle;
            }
            else
            {
                gizmo.Hide();
            }

            gizmo.Highlight(hovered);

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
                .ToList();
            var drag = CreateDrag(_context.TransformMode, handle, pivot, ray);
            if (originals.Count == 0 || drag == null)
                return;

            _drag = new ActiveDrag(drag, _gizmos[_context.TransformMode], originals);
        }

        // Which drag a handle starts depends on the mode; null if the ray can't start it.
        ITransformDrag CreateDrag(TransformMode mode, GizmoHandle handle, System.Numerics.Vector3 pivot, Ray ray)
        {
            var origin = ray.origin.ToNumerics();
            var direction = ray.direction.ToNumerics();
            var axis = handle.Direction().ToNumerics();

            switch (mode)
            {
                case TransformMode.Move:
                    var moveConstraint = handle.IsAxis()
                        ? DragConstraint.Line(pivot, axis)
                        : DragConstraint.Plane(pivot, System.Numerics.Vector3.UnitY);
                    return MoveDrag.TryBegin(moveConstraint, origin, direction);

                case TransformMode.Rotate:
                    return handle.IsAxis() ? RotateDrag.TryBegin(pivot, axis, origin, direction) : null;

                case TransformMode.Scale:
                    // The center handle scales by distance on a plane facing the camera.
                    var scaleConstraint = handle.IsAxis()
                        ? DragConstraint.Line(pivot, axis)
                        : DragConstraint.Plane(pivot, -_camera.transform.forward.ToNumerics());
                    return ScaleDrag.TryBegin(scaleConstraint, origin, direction);

                default:
                    return null;
            }
        }

        void TickDrag(in ToolInput input)
        {
            if (input.Cancel)
            {
                CancelDrag();
                _context.Report("Transform cancelled");
                return;
            }

            var drag = _drag.Drag;
            drag.Update(input.Ray.origin.ToNumerics(), input.Ray.direction.ToNumerics(), input.Snap);
            foreach (var original in _drag.Originals)
                _view.PreviewObject(drag.Apply(original));
            _drag.Gizmo.Show(drag.HandlePosition.ToUnity(), _camera);
            _context.Report($"{drag.Verb} {drag.Readout}");

            if (input.PrimaryReleased || !input.PrimaryHeld)
                CommitDrag();
        }

        void CommitDrag()
        {
            var active = _drag;
            _drag = null;
            var drag = active.Drag;
            var ids = active.Originals.Select(p => p.Id).ToList();
            if (drag.IsIdentity)
            {
                _view.ResetTransforms(ids);
                return;
            }

            var changed = 0;
            if (_context.TryEdit(drag.Verb, () => changed = ObjectEdits.Transform(_context.Session, ids, drag.Apply, drag.Verb)))
                _context.Report($"{drag.Verb} {drag.Readout}: {changed} object(s)");
            else
                _view.ResetTransforms(ids);
        }

        void CancelDrag()
        {
            if (_drag == null)
                return;
            _view.ResetTransforms(_drag.Originals.Select(p => p.Id));
            _drag = null;
        }

        Gizmo ShowOnly(TransformMode mode)
        {
            foreach (var pair in _gizmos)
            {
                if (pair.Key != mode)
                    pair.Value.Hide();
            }

            return _gizmos[mode];
        }

        void HideAll()
        {
            foreach (var gizmo in _gizmos.Values)
                gizmo.Hide();
        }

        sealed class ActiveDrag
        {
            public ActiveDrag(ITransformDrag drag, Gizmo gizmo, IReadOnlyList<PlacedObjectRecord> originals)
            {
                Drag = drag;
                Gizmo = gizmo;
                Originals = originals;
            }

            public ITransformDrag Drag { get; }

            public Gizmo Gizmo { get; }

            /// <summary>The selected objects as they were when the drag began.</summary>
            public IReadOnlyList<PlacedObjectRecord> Originals { get; }
        }
    }
}
