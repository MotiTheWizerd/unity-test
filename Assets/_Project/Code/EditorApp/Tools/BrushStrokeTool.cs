using System;
using Forge.EditorApp.Gizmos;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// A terrain brush: hold the left button on the terrain to stroke. The ring shows the brush; dabs are emitted at a
    /// fixed rate (frame-rate independent) while the button is held, previewed live by the tool, and committed as one
    /// undo step on release. Esc cancels a stroke, or returns to the Select tool. Sculpt and paint differ only in what
    /// a stroke does (the abstract members).
    /// </summary>
    public abstract class BrushStrokeTool : IEditorTool, IDisposable
    {
        const float DabsPerSecond = 30f;
        const int MaxDabsPerFrame = 5;

        readonly ScenePicker _picker;
        readonly BrushCursor _cursor;
        bool _stroking;
        float _dabTimer;

        /// <param name="worldRoot">What the brush ring is drawn under.</param>
        protected BrushStrokeTool(EditorContext context, ScenePicker picker, Transform worldRoot)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _cursor = new BrushCursor(new TerrainSurface(picker), worldRoot);
        }

        protected EditorContext Context { get; }

        /// <summary>The action's name, for reports and refused edits ("Sculpt", "Paint").</summary>
        protected abstract string ActionName { get; }

        /// <summary>The ring's color now.</summary>
        protected abstract Color RingColor { get; }

        public abstract void Activate();

        public void Deactivate()
        {
            CancelStroke();
            _cursor.Hide();
        }

        public virtual void Dispose() => _cursor.Dispose();

        public void Tick(in ToolInput input)
        {
            if (input.Cancel)
            {
                if (_stroking)
                {
                    CancelStroke();
                    Context.Report($"{ActionName} cancelled");
                }
                else
                {
                    Context.SelectTool(ToolKind.Select);
                }

                return;
            }

            var onTerrain = _picker.TryPickTerrain(input.Ray, out var hit);
            if (onTerrain)
                _cursor.Show(hit.Point, Context.BrushRadius, RingColor);
            else
                _cursor.Hide();

            if (!_stroking)
            {
                if (input.PrimaryPressed && onTerrain && BeginStroke(Flat(hit.Point)))
                {
                    _stroking = true;
                    _dabTimer = 0f;
                    Dab(Flat(hit.Point));
                }

                return;
            }

            if (input.PrimaryHeld && onTerrain)
            {
                _dabTimer += input.DeltaTime;
                var interval = 1f / DabsPerSecond;
                for (var i = 0; i < MaxDabsPerFrame && _dabTimer >= interval; i++)
                {
                    _dabTimer -= interval;
                    Dab(Flat(hit.Point));
                }

                // Drop backlog the cap skipped, so a hitch doesn't cause a burst later.
                _dabTimer = Math.Min(_dabTimer, interval);
            }

            if (input.PrimaryReleased || !input.PrimaryHeld)
                CommitStroke();
        }

        /// <summary>Starts a stroke at world XZ <paramref name="point"/>; false when it can't (and says why).</summary>
        protected abstract bool BeginStroke(System.Numerics.Vector2 point);

        /// <summary>One dab at world XZ <paramref name="point"/>, previewed in the scene.</summary>
        protected abstract void Dab(System.Numerics.Vector2 point);

        /// <summary>Writes the stroke to the session as one undo step; returns a report for the status line, or null.</summary>
        protected abstract string Commit();

        /// <summary>Puts the scene back as the records say, discarding the stroke's preview.</summary>
        protected abstract void ResetPreview();

        /// <summary>Forgets the stroke, committed or not.</summary>
        protected abstract void EndStroke();

        void CommitStroke()
        {
            _stroking = false;
            string report = null;
            if (Context.TryEdit(ActionName, () => report = Commit()))
            {
                if (report != null)
                    Context.Report(report);
            }
            else
            {
                ResetPreview();
            }

            EndStroke();
        }

        void CancelStroke()
        {
            if (!_stroking)
                return;
            _stroking = false;
            ResetPreview();
            EndStroke();
        }

        static System.Numerics.Vector2 Flat(Vector3 point) => new System.Numerics.Vector2(point.x, point.z);
    }
}
