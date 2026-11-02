using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Placement;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    /// <summary>
    /// A small world for the building commands: a pine (random turn and size, upright), a fence (follows the
    /// slope) and a house (exact), on terrain covering [0, 100] on both axes that rises 1 m per 10 m eastwards.
    /// </summary>
    sealed class BuildingFixture
    {
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

        BuildingFixture()
        {
            Session = new EditSession(
                new[]
                {
                    Plugin("core",
                        new BaseObjectRecord(Tree, "Pine Tree", "tree", "Nature/Trees", new PlacementRules(true, 0.8f, 1.2f, false)),
                        new BaseObjectRecord(Fence, "Wooden Fence", "fence", "Architecture/Walls", new PlacementRules(false, 1f, 1f, true)),
                        new BaseObjectRecord(House, "Small House", "house", "Architecture/Buildings")),
                },
                Plugin("user", new[] { "core" }));
            Options = new PlacementOptions(new Random(1));
            var seating = new ObjectSeating(Ground, SlopeShapeOf, key => ShapesByKey[key]);
            Context = new BuildContext(Session, seating, Options);
            BuildingCommands.AddTo(Registry, Context);
        }

        public static BuildingFixture Create() => new BuildingFixture();

        public EditSession Session { get; }

        public PlacementOptions Options { get; }

        public BuildContext Context { get; }

        public CommandRegistry Registry { get; } = new CommandRegistry();

        public static float? Ground(Vector2 p) =>
            p.X >= 0f && p.X <= 100f && p.Y >= 0f && p.Y <= 100f ? 10f + 0.1f * p.X : (float?)null;

        // What tilts, decided the way the editor's SlopeAlignment does: the rules and the toggle.
        PlacementShape? SlopeShapeOf(RecordId baseId) =>
            Session.World.Records.TryGet(baseId, out var record) && record is BaseObjectRecord baseObject && Options.TiltsToSlope(baseObject)
                ? ShapesByKey[baseObject.AssetKey]
                : (PlacementShape?)null;
    }
}
