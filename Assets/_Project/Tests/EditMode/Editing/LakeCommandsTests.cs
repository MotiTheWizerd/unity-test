using System;
using System.Linq;
using Forge.Data.Records;
using Forge.Editing;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Placement;
using Forge.Editing.Water;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>Lakes through the agent's commands, over <see cref="PitTerrain"/>.</summary>
    public class LakeCommandsTests
    {
        static BuildContext Context(EditSession session)
        {
            var terrain = PitTerrain.Grid(session);
            var seating = new ObjectSeating(terrain.HeightAt, _ => null, _ => new PlacementShape(new Footprint(1f, 1f), 0f));
            return new BuildContext(session, () => PitTerrain.Space, seating, new PlacementOptions(new Random(1)));
        }

        static AddLakeCommand.Result AddLake(BuildContext context, float x, float z, float? level = null) =>
            new AddLakeCommand(context).Execute(new AddLakeCommand.Arguments { X = x, Z = z, Level = level });

        [Test]
        public void AddLake_WithoutALevel_FillsTheHollowToJustBelowItsRim()
        {
            var context = Context(PitTerrain.Session());

            var result = AddLake(context, 64f, 64f);

            Assert.That(result.Id, Is.EqualTo("user:lake/000001"));
            Assert.That(result.Level, Is.EqualTo(PitTerrain.Gutter - LakeBasin.FullBelowBrim));
            Assert.That(result.Rim, Is.EqualTo(PitTerrain.Gutter));
            Assert.That(result.Area, Is.EqualTo(PitTerrain.PitSamples));
            Assert.That(result.Summary, Does.Contain("it can rise to 45 m"));
            Assert.That(context.Water.DepthAt(PitTerrain.PitCenter), Is.EqualTo(4.75f));
        }

        [Test]
        public void AddLake_Refusals_SayWhatWouldWork()
        {
            var context = Context(PitTerrain.Session());

            Assert.That(() => AddLake(context, 20f, 20f), Throws.TypeOf<CommandException>().With.Message.Contains("isn't in a hollow"));
            Assert.That(() => AddLake(context, 64f, 64f, 47f), Throws.TypeOf<CommandException>()
                .With.Message.Contains("only up to 45 m").And.Message.Not.Contains("Parameter"));
            Assert.That(() => AddLake(context, -40f, 20f), Throws.TypeOf<CommandException>().With.Message.Contains("no terrain"));
        }

        [Test]
        public void SetLakeLevel_AndRemoveLake_WorkByTheLakesId()
        {
            var session = PitTerrain.Session();
            var context = Context(session);
            var id = AddLake(context, 64f, 64f, 42f).Id;

            new SetLakeLevelCommand(context).Execute(new SetLakeLevelCommand.Arguments { Id = id, Level = 44f });
            Assert.That(session.World.Records.OfType<LakeRecord>().Single().Level, Is.EqualTo(44f));

            new RemoveLakeCommand(context).Execute(new RemoveLakeCommand.Arguments { Id = id });
            Assert.That(session.World.Records.OfType<LakeRecord>(), Is.Empty);
            Assert.That(() => new RemoveLakeCommand(context).Execute(new RemoveLakeCommand.Arguments { Id = id }),
                Throws.TypeOf<CommandException>().With.Message.Contains("inspect_area"));
        }

        [Test]
        public void InspectArea_ListsTheLakes_AndCountsTheirWater()
        {
            var context = Context(PitTerrain.Session());
            var id = AddLake(context, 64f, 64f, 44f).Id;

            // A 42 m square over the pit (21 × 21 samples): x and z from 43 to 85, 43 × 43 samples.
            var result = new InspectAreaCommand(context).Execute(new InspectAreaCommand.Arguments { X = 64f, Z = 64f, Size = 42f });

            Assert.That(result.Water.SeaLevel, Is.Null);
            Assert.That(result.Water.Lakes.Select(l => (l.Id, l.Level, l.Spills)), Is.EqualTo(new[] { (id, 44f, false) }));
            Assert.That(result.Water.UnderwaterPercent, Is.EqualTo((int)Math.Round(100.0 * PitTerrain.PitSamples / (43 * 43))));
            Assert.That(result.Water.Deepest, Is.EqualTo(4f));
            Assert.That(result.Summary, Does.Contain($"lake {id} at 44 m"));
        }
    }
}
