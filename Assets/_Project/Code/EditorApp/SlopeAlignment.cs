using System;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Placement;

namespace Forge.EditorApp
{
    /// <summary>
    /// Decides which objects are seated tilted to the slope (their base object's rules ask for it and
    /// the editor's "Align to slope" toggle is on) and supplies their shape. Placing, dropping and
    /// sculpting all ask here, so they agree on what tilts.
    /// </summary>
    public sealed class SlopeAlignment
    {
        readonly EditorContext _context;
        readonly AssetShapes _shapes;

        public SlopeAlignment(EditorContext context, AssetShapes shapes)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _shapes = shapes ?? throw new ArgumentNullException(nameof(shapes));
        }

        /// <summary>The shape to seat objects of <paramref name="baseId"/> with, or null if they stay upright.</summary>
        public PlacementShape? ShapeToAlign(RecordId baseId) =>
            _context.AlignsToSlope(baseId) && _context.Session.World.Records.TryGet(baseId, out var record) && record is BaseObjectRecord baseObject
                ? _shapes.ShapeOf(baseObject.AssetKey)
                : (PlacementShape?)null;

        public PlacementShape? ShapeToAlign(PlacedObjectRecord placed) => ShapeToAlign(placed.BaseId);
    }
}
