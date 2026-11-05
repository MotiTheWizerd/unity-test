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
    /// Hold the left button on the terrain to sculpt with the current brush: dabs go into a <see cref="SculptStroke"/>,
    /// placed objects follow the ground (when on), and release commits it all as one undo step.
    /// </summary>
    public sealed class SculptTool : BrushStrokeTool
    {
        readonly WorldView _view;
        readonly SlopeAlignment _slopes;
        readonly HashSet<RecordId> _previewedObjects = new HashSet<RecordId>();
        SculptStroke _stroke;
        TerrainFollower _follower;

        /// <param name="slopes">Which followed objects are also re-tilted to the new ground (fences, rocks).</param>
        /// <param name="worldRoot">What the brush ring is drawn under.</param>
        public SculptTool(EditorContext context, WorldView view, ScenePicker picker, SlopeAlignment slopes, Transform worldRoot)
            : base(context, picker, worldRoot)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _slopes = slopes ?? throw new ArgumentNullException(nameof(slopes));
        }

        protected override string ActionName => "Sculpt";

        protected override Color RingColor => BrushCursor.ColorFor(Context.BrushMode);

        public override void Activate() => Context.Report($"Sculpt: hold left mouse on the terrain ({Context.BrushMode})");

        protected override bool BeginStroke(System.Numerics.Vector2 point)
        {
            var brush = Context.CurrentBrush();
            var world = Context.Session.World.Records;
            var space = Context.ActiveSpace;
            Context.TryEdit("Sculpt", () => _stroke = new SculptStroke(world, space, brush, point));
            if (_stroke == null)
                return false;

            _follower = Context.ObjectsFollowTerrain ? new TerrainFollower(world, space, _slopes.ShapeToAlign) : null;
            return true;
        }

        protected override void Dab(System.Numerics.Vector2 point)
        {
            foreach (var coord in _stroke.ApplyDab(point))
                _view.PreviewTerrain(coord, _stroke.HeightsOf(coord));

            if (_follower == null)
                return;
            foreach (var placed in _follower.Follow(_stroke))
            {
                _view.PreviewObject(placed);
                _previewedObjects.Add(placed.Id);
            }
        }

        protected override string Commit()
        {
            var chunks = TerrainEdits.Commit(Context.Session, _stroke, _follower);
            // The committed records now drive the scene; nothing left to reset.
            _previewedObjects.Clear();
            return chunks > 0 ? $"Sculpted {chunks} chunk(s) ({_stroke.Brush.Mode})" : null;
        }

        protected override void ResetPreview()
        {
            if (_stroke != null)
                _view.ResetTerrain(_stroke.TouchedChunks);
            _view.ResetTransforms(_previewedObjects);
            _previewedObjects.Clear();
        }

        protected override void EndStroke()
        {
            _stroke = null;
            _follower = null;
        }
    }
}
