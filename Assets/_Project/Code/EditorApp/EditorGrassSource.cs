using System;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.World;

namespace Forge.EditorApp
{
    /// <summary>
    /// What the editor's grass grows from: the land the world view shows (the winning chunk records of the active
    /// space), the ground layers of the current world, the unpainted ground layer and the active space's sea. Read live,
    /// so an edit, an undo or a new sea level is picked up the next time a patch is checked.
    /// </summary>
    /// <remarks>Lakes don't stop grass yet: only the sea does (a lake's flooded area isn't known to the view).</remarks>
    sealed class EditorGrassSource : IGrassSource
    {
        readonly EditorContext _context;
        readonly WorldView _view;

        public EditorGrassSource(EditorContext context, WorldView view)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public TerrainChunkRecord ChunkAt(ChunkCoord coord) => _view.TryGetTerrain(coord, out var chunk) ? chunk : null;

        public GrassSettings GrassOf(RecordId layer) =>
            _context.Session.World.Records.TryGet(layer, out var record) && record is GroundLayerRecord ground ? ground.Grass : null;

        public RecordId? UnpaintedGround => _context.UnpaintedGround;

        public float? WaterLevel => _context.ActiveSpaceRecord?.WaterLevel;
    }
}
