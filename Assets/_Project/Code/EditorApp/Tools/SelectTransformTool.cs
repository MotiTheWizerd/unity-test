using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Editing;
using Forge.Editing.Geometry;
using Forge.Editing.Placement;
using Forge.Editing.Transforms;
using Forge.EditorApp.Gizmos;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// The select tool and the three transform tools, which share picking: click to select (Shift adds or removes),
    /// Del deletes the selection, Esc cancels a drag or clears the selection.
    /// <list type="bullet">
    /// <item><see cref="ToolKind.Select"/> only selects.</item>
    /// <item><see cref="ToolKind.Move"/> also drags objects themselves over the ground (<see cref="GroundSlide"/>:
    /// pressing an object selects it if it isn't already, and dragging slides the whole selection), or the gizmo's
    /// arrows along one axis and its ball across the level.</item>
    /// <item><see cref="ToolKind.Rotate"/> and <see cref="ToolKind.Scale"/> drag their gizmo's rings and cubes.</item>
    /// </list>
    /// Ctrl snaps. A drag previews on the scene objects and commits one undoable edit on release.
    /// </summary>
    public sealed class SelectTransformTool : IEditorTool, IDisposable
    {
        // A press on an object only becomes a slide once the pointer has moved this far, so a click that selects
        // never nudges the object by a hand's jitter.
        const float SlideDeadZonePixels = 4f;

        readonly ToolKind _kind;
        readonly EditorContext _context;
        readonly WorldView _view;
        readonly ScenePicker _picker;
        readonly Camera _camera;
        readonly ObjectSeating _seating;
        readonly Gizmo _gizmo;
        ActiveDrag _drag;

        /// <param name="kind">Select, Move, Rotate or Scale.</param>
        /// <param name="gizmoMaterial">Material for the gizmo (the always-on-top overlay); null for the default.</param>
        /// <param name="seating">How sliding objects are set down on the ground, exactly as placing does.</param>
        public SelectTransformTool(ToolKind kind, EditorContext context, WorldView view, ScenePicker picker, Camera camera,
            Material gizmoMaterial, ObjectSeating seating)
        {
            _kind = kind;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _seating = seating ?? throw new ArgumentNullException(nameof(seating));
            _gizmo = CreateGizmo(kind, picker.GizmoLayer, gizmoMaterial);
        }

        // The controller shows the tool's hint (ToolCatalog).
        public void Activate()
        {
        }

        public void Deactivate()
        {
            CancelDrag();
            _gizmo?.Hide();
        }

        public void Dispose() => _gizmo?.Dispose();

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
            var hovered = HoveredHandle(input.Ray, pivot);
            if (!input.PrimaryPressed)
                return;

            if (hovered.HasValue && pivot.HasValue)
                BeginHandleDrag(hovered.Value, pivot.Value, input.Ray);
            else
                PressInWorld(input);
        }

        // Shows the gizmo on the selection and returns the handle under the pointer, if any.
        GizmoHandle? HoveredHandle(Ray ray, System.Numerics.Vector3? pivot)
        {
            if (_gizmo == null)
                return null;

            GizmoHandle? hovered = null;
            if (pivot.HasValue)
            {
                _gizmo.Show(pivot.Value.ToUnity(), _camera);
                if (_picker.TryPickHandle(ray, out var handle))
                    hovered = handle;
            }
            else
            {
                _gizmo.Hide();
            }

            _gizmo.Highlight(hovered);
            return hovered;
        }

        void PressInWorld(in ToolInput input)
        {
            var selection = _context.Selection;
            if (!_picker.TryPickPlacedObject(input.Ray, _context.Session.World.Records, out var id, out var point))
            {
                if (!input.Additive)
                    selection.Clear();
                return;
            }

            if (input.Additive)
            {
                selection.Toggle(id);
                return;
            }

            // Moving keeps a selection the press is part of, so the whole group slides together.
            if (_kind != ToolKind.Move || !selection.Contains(id))
                selection.Set(id);
            if (_kind == ToolKind.Move)
                BeginSlide(point, input);
        }

        void BeginSlide(Vector3 grabPoint, in ToolInput input)
        {
            var pivot = ObjectEdits.Pivot(_context.Session, _context.Selection.Ids);
            if (!pivot.HasValue)
                return;

            var records = _context.Session.World.Records;
            var slide = GroundSlide.TryBegin(pivot.Value, grabPoint.ToNumerics(), input.Ray.origin.ToNumerics(), input.Ray.direction.ToNumerics(),
                _seating, id => records.TryGet(id, out var record) ? record as BaseObjectRecord : null);
            Begin(slide, input.Pointer, SlideDeadZonePixels);
        }

        void BeginHandleDrag(GizmoHandle handle, System.Numerics.Vector3 pivot, Ray ray) =>
            Begin(CreateHandleDrag(handle, pivot, ray), Vector2.zero, deadZonePixels: 0f);

        void Begin(ITransformDrag drag, Vector2 pressPointer, float deadZonePixels)
        {
            var originals = _context.Selection.Ids
                .Select(id => _context.Session.World.Records.TryGet(id, out var r) ? r as PlacedObjectRecord : null)
                .Where(p => p != null)
                .ToList();
            if (originals.Count == 0 || drag == null)
                return;

            _drag = new ActiveDrag(drag, originals, pressPointer, deadZonePixels);
        }

        // Which drag a handle starts depends on the tool; null if the ray can't start it.
        ITransformDrag CreateHandleDrag(GizmoHandle handle, System.Numerics.Vector3 pivot, Ray ray)
        {
            var origin = ray.origin.ToNumerics();
            var direction = ray.direction.ToNumerics();
            var axis = handle.Direction().ToNumerics();

            switch (_kind)
            {
                case ToolKind.Move:
                    var moveConstraint = handle.IsAxis()
                        ? DragConstraint.Line(pivot, axis)
                        : DragConstraint.Plane(pivot, System.Numerics.Vector3.UnitY);
                    return MoveDrag.TryBegin(moveConstraint, origin, direction);

                case ToolKind.Rotate:
                    return handle.IsAxis() ? RotateDrag.TryBegin(pivot, axis, origin, direction) : null;

                case ToolKind.Scale:
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

            var released = input.PrimaryReleased || !input.PrimaryHeld;
            if (!_drag.Started)
            {
                // Still a click: let go now and nothing moved.
                if (released)
                {
                    _drag = null;
                    return;
                }

                if (Vector2.Distance(input.Pointer, _drag.PressPointer) < _drag.DeadZonePixels)
                    return;
                _drag.Started = true;
            }

            var drag = _drag.Drag;
            drag.Update(input.Ray.origin.ToNumerics(), input.Ray.direction.ToNumerics(), input.Snap);
            foreach (var original in _drag.Originals)
                _view.PreviewObject(drag.Apply(original));
            _gizmo?.Show(drag.HandlePosition.ToUnity(), _camera);
            _context.Report($"{drag.Verb} {drag.Readout}");

            if (released)
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
            if (_drag.Started)
                _view.ResetTransforms(_drag.Originals.Select(p => p.Id));
            _drag = null;
        }

        static Gizmo CreateGizmo(ToolKind kind, int layer, Material material)
        {
            switch (kind)
            {
                case ToolKind.Select: return null;
                case ToolKind.Move: return GizmoShapes.Translate(layer, material);
                case ToolKind.Rotate: return GizmoShapes.Rotate(layer, material);
                case ToolKind.Scale: return GizmoShapes.Scale(layer, material);
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a select or transform tool.");
            }
        }

        sealed class ActiveDrag
        {
            public ActiveDrag(ITransformDrag drag, IReadOnlyList<PlacedObjectRecord> originals, Vector2 pressPointer, float deadZonePixels)
            {
                Drag = drag;
                Originals = originals;
                PressPointer = pressPointer;
                DeadZonePixels = deadZonePixels;
                Started = deadZonePixels <= 0f;
            }

            public ITransformDrag Drag { get; }

            /// <summary>The selected objects as they were when the drag began.</summary>
            public IReadOnlyList<PlacedObjectRecord> Originals { get; }

            /// <summary>Where the pointer pressed, in screen pixels.</summary>
            public Vector2 PressPointer { get; }

            /// <summary>How far the pointer must move before the drag starts changing anything.</summary>
            public float DeadZonePixels { get; }

            /// <summary>Whether the pointer has left the dead zone, so the drag previews and commits.</summary>
            public bool Started { get; set; }
        }
    }
}
