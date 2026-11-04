using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Editing.Terrain;
using Forge.EditorApp.Gizmos;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Hold the left button on the terrain to sculpt with the current brush.
    /// Dabs are emitted at a fixed rate (frame-rate independent) into a
    /// <see cref="SculptStroke"/>, previewed live, and committed as one undo step
    /// on release. Esc cancels a stroke, or returns to the Select tool.
    /// </summary>
    public sealed class SculptTool : IEditorTool, IDisposable
    {
        const float DabsPerSecond = 30f;
        const int MaxDabsPerFrame = 5;

        readonly EditorContext _context;
        readonly WorldView _view;
        readonly ScenePicker _picker;
        readonly BrushCursor _cursor;
        readonly SlopeAlignment _slopes;
        readonly HashSet<RecordId> _previewedObjects = new HashSet<RecordId>();
        SculptStroke _stroke;
        TerrainFollower _follower;
        float _dabTimer;

        /// <param name="slopes">Which followed objects are also re-tilted to the new ground (fences, rocks).</param>
        public SculptTool(EditorContext context, WorldView view, ScenePicker picker, SlopeAlignment slopes)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _slopes = slopes ?? throw new ArgumentNullException(nameof(slopes));
            _cursor = new BrushCursor(new TerrainSurface(picker));
        }

        public void Activate() => _context.Report($"Sculpt: hold left mouse on the terrain ({_context.BrushMode})");

        public void Deactivate()
        {
            CancelStroke();
            _cursor.Hide();
        }

        public void Dispose() => _cursor.Dispose();

        public void Tick(in ToolInput input)
        {
            if (input.Cancel)
            {
                if (_stroke != null)
                {
                    CancelStroke();
                    _context.Report("Sculpt cancelled");
                }
                else
                {
                    _context.ActiveTool = ToolKind.Select;
                }

                return;
            }

            var onTerrain = _picker.TryPickTerrain(input.Ray, out var hit);
            if (onTerrain)
                _cursor.Show(hit.point, _context.BrushRadius, _context.BrushMode);
            else
                _cursor.Hide();

            if (_stroke == null)
            {
                if (input.PrimaryPressed && onTerrain)
                    BeginStroke(hit.point);
                return;
            }

            if (input.PrimaryHeld && onTerrain)
            {
                _dabTimer += input.DeltaTime;
                var interval = 1f / DabsPerSecond;
                for (var i = 0; i < MaxDabsPerFrame && _dabTimer >= interval; i++)
                {
                    _dabTimer -= interval;
                    Dab(hit.point);
                }

                // Drop backlog the cap skipped, so a hitch doesn't cause a burst later.
                _dabTimer = Math.Min(_dabTimer, interval);
            }

            if (input.PrimaryReleased || !input.PrimaryHeld)
                CommitStroke();
        }

        void BeginStroke(Vector3 point)
        {
            var brush = _context.CurrentBrush();
            var world = _context.Session.World.Records;
            var space = _context.ActiveSpace;
            _context.TryEdit("Sculpt", () => _stroke = new SculptStroke(world, space, brush, Flat(point)));
            if (_stroke == null)
                return;

            _follower = _context.ObjectsFollowTerrain ? new TerrainFollower(world, space, _slopes.ShapeToAlign) : null;
            _dabTimer = 0f;
            Dab(point);
        }

        void Dab(Vector3 point)
        {
            foreach (var coord in _stroke.ApplyDab(Flat(point)))
                _view.PreviewTerrain(coord, _stroke.HeightsOf(coord));

            if (_follower == null)
                return;
            foreach (var placed in _follower.Follow(_stroke))
            {
                _view.PreviewObject(placed);
                _previewedObjects.Add(placed.Id);
            }
        }

        void CommitStroke()
        {
            var stroke = _stroke;
            var follower = _follower;
            EndStroke();
            var chunks = 0;
            if (_context.TryEdit("Sculpt", () => chunks = TerrainEdits.Commit(_context.Session, stroke, follower)))
            {
                // The committed records now drive the scene; nothing left to reset.
                _previewedObjects.Clear();
                if (chunks > 0)
                    _context.Report($"Sculpted {chunks} chunk(s) ({stroke.Brush.Mode})");
            }
            else
            {
                ResetPreview(stroke);
            }
        }

        void CancelStroke()
        {
            if (_stroke == null)
                return;
            var stroke = _stroke;
            EndStroke();
            ResetPreview(stroke);
        }

        void EndStroke()
        {
            _stroke = null;
            _follower = null;
        }

        void ResetPreview(SculptStroke stroke)
        {
            _view.ResetTerrain(stroke.TouchedChunks);
            _view.ResetTransforms(_previewedObjects);
            _previewedObjects.Clear();
        }

        static System.Numerics.Vector2 Flat(Vector3 point) => new System.Numerics.Vector2(point.x, point.z);
    }
}
