using System;
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
        SculptStroke _stroke;
        float _dabTimer;

        public SculptTool(EditorContext context, WorldView view, ScenePicker picker)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _cursor = new BrushCursor(ray => _picker.TryPickTerrain(ray, out var hit) ? (true, hit.point) : (false, default(Vector3)));
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
            _context.TryEdit("Sculpt", () =>
                _stroke = new SculptStroke(_context.Session.World.Records, brush, Flat(point)));
            if (_stroke == null)
                return;

            _dabTimer = 0f;
            Dab(point);
        }

        void Dab(Vector3 point)
        {
            foreach (var coord in _stroke.ApplyDab(Flat(point)))
                _view.PreviewTerrain(coord, _stroke.HeightsOf(coord));
        }

        void CommitStroke()
        {
            var stroke = _stroke;
            _stroke = null;
            var chunks = 0;
            if (_context.TryEdit("Sculpt", () => chunks = TerrainEdits.Commit(_context.Session, stroke)))
            {
                if (chunks > 0)
                    _context.Report($"Sculpted {chunks} chunk(s) ({stroke.Brush.Mode})");
            }
            else
            {
                _view.ResetTerrain(stroke.TouchedChunks);
            }
        }

        void CancelStroke()
        {
            if (_stroke == null)
                return;
            _view.ResetTerrain(_stroke.TouchedChunks);
            _stroke = null;
        }

        static System.Numerics.Vector2 Flat(Vector3 point) => new System.Numerics.Vector2(point.x, point.z);
    }
}
