using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.EditorApp.Tools;
using Forge.Editing;
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

        public EditorCommands(EditorContext context, WorldView view, ScenePicker picker, ICameraRig camera)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
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
        /// (or above, if it is buried). One undo step for the whole selection.
        /// </summary>
        public void DropSelectionToGround()
        {
            if (_context.Selection.IsEmpty)
            {
                _context.Report("Drop to ground: nothing selected");
                return;
            }

            var positions = new Dictionary<RecordId, System.Numerics.Vector3>();
            var missed = 0;
            foreach (var id in _context.Selection.Ids)
            {
                if (!_view.TryGetObject(id, out var go) || !RendererBounds.TryGet(go, out var bounds))
                    continue;

                var probe = new Ray(new Vector3(go.transform.position.x, bounds.max.y + ProbeHeight, go.transform.position.z), Vector3.down);
                if (!_picker.TryPickTerrain(probe, out var ground))
                {
                    missed++;
                    continue;
                }

                // Keep the pivot-to-bottom distance so the mesh's lowest point lands on the surface.
                var pivotAboveBottom = go.transform.position.y - bounds.min.y;
                var target = go.transform.position;
                target.y = ground.point.y + pivotAboveBottom;
                positions[id] = target.ToNumerics();
            }

            var moved = 0;
            if (_context.TryEdit("Drop to ground", () => moved = ObjectEdits.SetPositions(_context.Session, positions, "Drop")))
            {
                _context.Report(missed == 0
                    ? $"Dropped {moved} object(s) to ground"
                    : $"Dropped {moved} object(s); {missed} had no terrain below");
            }
        }
    }
}
