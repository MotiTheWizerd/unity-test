using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Placement;
using Forge.Editing.Terrain;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    /// <summary>
    /// A small world for the building commands: a pine (random turn and size, upright), a fence (follows the
    /// slope) and a house (exact), on four terrain chunks of <see cref="Space"/> covering [0, 127] on both axes
    /// (1 m samples) that rise 1 m per 10 m eastwards. Objects are seated on that terrain's data, standing in for
    /// the scene's probe. The commands build in <see cref="Space"/>.
    /// </summary>
    sealed class BuildingFixture
    {
        /// <summary>Where the terrain data reaches; the last sample row (x or z = 128) belongs to no chunk.</summary>
        public const float TerrainEdge = 127f;

        /// <summary>The space the fixture's terrain belongs to and the commands build in.</summary>
        public static readonly RecordId Space = Forge.Tests.Data.TestRecords.Space;

        public static readonly RecordId Tree = RecordId.Parse("core:tree");
        public static readonly RecordId Fence = RecordId.Parse("core:fence");
        public static readonly RecordId House = RecordId.Parse("core:house");

        public static readonly PlacementShape TreeShape = new PlacementShape(new Footprint(2f, 2f), 0f);
        public static readonly PlacementShape FenceShape = new PlacementShape(new Footprint(3f, 0.2f), 0f);
        public static readonly PlacementShape HouseShape = new PlacementShape(new Footprint(4f, 5f), 0f);

        static readonly Dictionary<string, PlacementShape> ShapesByKey = new Dictionary<string, PlacementShape>
        {
            { "tree", TreeShape },
            { "fence", FenceShape },
            { "house", HouseShape },
        };

        BuildingFixture(Record[] extra)
        {
            var records = new List<Record>
            {
                new BaseObjectRecord(Tree, "Pine Tree", "tree", "Nature/Trees", new PlacementRules(true, 0.8f, 1.2f, false)),
                new BaseObjectRecord(Fence, "Wooden Fence", "fence", "Architecture/Walls", new PlacementRules(false, 1f, 1f, true)),
                new BaseObjectRecord(House, "Small House", "house", "Architecture/Buildings"),
            };
            for (var x = 0; x <= 1; x++)
            for (var z = 0; z <= 1; z++)
                records.Add(SlopeChunk(x, z));
            records.AddRange(extra);

            Session = new EditSession(new[] { Plugin("core", records.ToArray()) }, Plugin("user", new[] { "core" }));
            Options = new PlacementOptions(new Random(1));
            var terrain = new TerrainGrid(Session.World.Records, Space);
            var seating = new ObjectSeating(terrain.HeightAt, SlopeShapeOf, key => ShapesByKey[key]);
            Context = new BuildContext(Session, () => Space, seating, Options, SlotsOf);
            BuildingCommands.AddTo(Registry, Context);
        }

        /// <param name="extra">More "core" records, e.g. objects or terrain in another space.</param>
        public static BuildingFixture Create(params Record[] extra) => new BuildingFixture(extra);

        public EditSession Session { get; }

        public PlacementOptions Options { get; }

        public BuildContext Context { get; }

        public CommandRegistry Registry { get; } = new CommandRegistry();

        /// <summary>The terrain's height, as a formula: 10 m at x = 0, rising 1 m per 10 m eastwards.</summary>
        public static float? Ground(Vector2 p) =>
            p.X >= 0f && p.X < TerrainEdge && p.Y >= 0f && p.Y < TerrainEdge ? 10f + 0.1f * p.X : (float?)null;

        static TerrainChunkRecord SlopeChunk(int x, int z)
        {
            var coord = new ChunkCoord(x, z);
            var samples = WorldGrid.DefaultHeightmapResolution;
            var spacing = WorldGrid.ChunkSize / (samples - 1);
            return new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, coord), Space, coord,
                HeightField.FromFunction((sx, sz) => 10f + 0.1f * (x * WorldGrid.ChunkSize + sx * spacing), samples));
        }

        // The material slots each asset has, standing in for the editor's AssetSlots: the house has walls and a roof.
        static IReadOnlyList<string> SlotsOf(BaseObjectRecord baseObject) =>
            baseObject.AssetKey == "house" ? new[] { "Plaster", "Roof" }
            : baseObject.AssetKey == "fence" ? new[] { "Wood" }
            : Array.Empty<string>();

        // What tilts, decided the way the editor's SlopeAlignment does: the rules and the toggle.
        PlacementShape? SlopeShapeOf(RecordId baseId) =>
            Session.World.Records.TryGet(baseId, out var record) && record is BaseObjectRecord baseObject && Options.TiltsToSlope(baseObject)
                ? ShapesByKey[baseObject.AssetKey]
                : (PlacementShape?)null;
    }
}
