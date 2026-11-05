using System;
using Forge.Editing.Terrain;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Hold the left button on the terrain to paint the chosen ground layer (<see cref="EditorContext.PaintLayer"/>):
    /// dabs go into a <see cref="PaintStroke"/>, previewed on the chunks, and release commits them as one undo step.
    /// </summary>
    public sealed class PaintTool : BrushStrokeTool
    {
        static readonly Color Ring = new Color(0.95f, 0.78f, 0.3f);

        readonly WorldView _view;
        PaintStroke _stroke;

        /// <param name="worldRoot">What the brush ring is drawn under.</param>
        public PaintTool(EditorContext context, WorldView view, ScenePicker picker, Transform worldRoot)
            : base(context, picker, worldRoot)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        protected override string ActionName => "Paint";

        protected override Color RingColor => Ring;

        public override void Activate() => Context.Report(Context.PaintLayer.HasValue
            ? "Paint: hold left mouse on the terrain"
            : "Paint: this world has no ground layers to paint with");

        protected override bool BeginStroke(System.Numerics.Vector2 point)
        {
            if (!(Context.PaintLayer is Forge.Data.RecordId layer))
            {
                Context.Report("Paint: this world has no ground layers to paint with");
                return false;
            }

            if (!(Context.UnpaintedGround is Forge.Data.RecordId unpainted))
            {
                Context.Report("Paint: no ground layer looks like unpainted ground, so painting would change it");
                return false;
            }

            var shape = new BrushShape(Context.BrushRadius, Context.BrushStrength);
            Context.TryEdit("Paint", () => _stroke = new PaintStroke(Context.Session.World.Records, Context.ActiveSpace, layer, shape, unpainted));
            return _stroke != null;
        }

        protected override void Dab(System.Numerics.Vector2 point)
        {
            foreach (var coord in _stroke.ApplyDab(point))
                _view.PreviewPaint(coord, _stroke.PaintOf(coord));
        }

        protected override string Commit()
        {
            var chunks = TerrainEdits.Commit(Context.Session, _stroke);
            return chunks > 0 ? $"Painted {_stroke.LayerName} on {chunks} chunk(s)" : null;
        }

        protected override void ResetPreview()
        {
            if (_stroke != null)
                _view.ResetTerrain(_stroke.TouchedChunks);
        }

        protected override void EndStroke() => _stroke = null;
    }
}
