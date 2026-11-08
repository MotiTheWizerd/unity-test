using System;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>scatter_objects and ground_at on the fixture's slope: 10 m at x = 0, rising 1 m per 10 m eastwards (5.7°).</summary>
    public class ScatterTests
    {
        static ScatterObjectsCommand.Result Scatter(BuildingFixture world, int count, float size = 60f, float? maxSlope = null,
            float? maxHeight = null, float? clustering = 0f, float? spacing = null, params RecordId[] kinds) =>
            new ScatterObjectsCommand(world.Context).Execute(new ScatterObjectsCommand.Arguments
            {
                Objects = (kinds.Length > 0 ? kinds : new[] { BuildingFixture.Tree }).Select(k => k.ToString()).ToList(),
                Count = count, X = 64f, Z = 64f, Width = size, Depth = size,
                MaxSlope = maxSlope, MaxHeight = maxHeight, Clustering = clustering, Spacing = spacing,
            });

        static Vector2[] Placed(BuildingFixture world) =>
            world.Session.World.Records.OfType<PlacedObjectRecord>().Select(p => new Vector2(p.Position.X, p.Position.Z)).ToArray();

        [Test]
        public void Scatter_PlacesThemInsideTheArea_KeepingTheirSpacing_AsOneUndoStep()
        {
            var world = BuildingFixture.Create();

            var result = Scatter(world, 30);

            var spots = Placed(world);
            Assert.That(result.Placed, Is.EqualTo(30));
            Assert.That(result.Summary, Does.StartWith("Scattered 30 object(s) over a 60 × 60 m area around (64, 64), heading 0°: 30 Pine Tree."));
            Assert.That(spots, Has.All.Matches<Vector2>(p => p.X >= 34f && p.X <= 94f && p.Y >= 34f && p.Y <= 94f));
            var closest = spots.SelectMany((a, i) => spots.Skip(i + 1).Select(b => Vector2.Distance(a, b))).Min();
            Assert.That(closest, Is.GreaterThanOrEqualTo(3f), "A pine is 2 m wide; the default spacing is a meter more.");
            Assert.That(world.Session.UndoDescription, Is.EqualTo("Scatter 30 objects"));
        }

        [Test]
        public void Scatter_KeepsOffTheGroundsToAvoid_AndSaysSoWhenTheyHeldItBack()
        {
            var dirt = GroundLayerRecord.IdFor("core", "dirt");
            var world = BuildingFixture.Create(
                new GroundLayerRecord(GroundLayerRecord.IdFor("core", "grass"), "Grass", BuildingFixture.BaseGroundLook),
                new GroundLayerRecord(dirt, "Dirt", "ground/dirt"));
            // A street 12 m wide, hard-edged, east-west through the middle of the area.
            new PaintLineCommand(world.Context).Execute(new PaintLineCommand.Arguments
            {
                Ground = "Dirt", Width = 12f, Edge = 0f,
                Points = new[] { new GroundPoint { X = 20f, Z = 64f }, new GroundPoint { X = 108f, Z = 64f } }.ToList(),
            });

            var result = new ScatterObjectsCommand(world.Context).Execute(new ScatterObjectsCommand.Arguments
            {
                Objects = new[] { BuildingFixture.Tree.ToString() }.ToList(), Count = 200, X = 64f, Z = 64f, Width = 30f, Depth = 30f,
                Clustering = 0f, AvoidGrounds = new[] { "dirt" }.ToList(),
            });

            var spots = Placed(world);
            Assert.That(spots, Is.Not.Empty);
            Assert.That(spots, Has.None.Matches<Vector2>(p => Math.Abs(p.Y - 64f) < 5.5f), "No tree stands in the street.");
            Assert.That(spots.Count(p => Math.Abs(p.Y - 64f) < 12f), Is.GreaterThan(0), "Trees still line it.");
            Assert.That(result.Placed, Is.LessThan(200), "More are asked than fit beside the street.");
            Assert.That(result.Summary, Does.Contain("the grounds to keep off"));
        }

        [Test]
        public void Scatter_AGroundToAvoidThatIsNotThere_PlacesNothing()
        {
            var scatter = new ScatterObjectsCommand(BuildingFixture.Create().Context);

            Assert.That(() => scatter.Execute(new ScatterObjectsCommand.Arguments
                {
                    Objects = new[] { BuildingFixture.Tree.ToString() }.ToList(), Count = 5, X = 64f, Z = 64f, Width = 20f, Depth = 20f,
                    AvoidGrounds = new[] { "Cobblestone" }.ToList(),
                }),
                Throws.TypeOf<CommandException>());
        }

        [Test]
        public void Scatter_KeepsToTheHeightRule()
        {
            var world = BuildingFixture.Create();

            Scatter(world, 20, maxHeight: 15f);

            Assert.That(Placed(world), Has.All.Matches<Vector2>(p => p.X <= 50.01f), "15 m of ground is at x = 50.");
        }

        [Test]
        public void Scatter_NothingFits_SaysWhichRuleHeldItBack()
        {
            var scatter = new ScatterObjectsCommand(BuildingFixture.Create().Context);

            Assert.That(() => scatter.Execute(new ScatterObjectsCommand.Arguments
                {
                    Objects = new[] { BuildingFixture.Tree.ToString() }.ToList(), Count = 5, X = 64f, Z = 64f, Width = 20f, Depth = 20f, MaxSlope = 3f,
                }),
                Throws.TypeOf<CommandException>().With.Message.Contains("the slope rule"));
        }

        [Test]
        public void Scatter_MoreThanFit_PlacesWhatFits_AndSaysSo()
        {
            var world = BuildingFixture.Create();

            var result = Scatter(world, 100, size: 10f);

            Assert.That(result.Placed, Is.LessThan(100).And.GreaterThan(3));
            Assert.That(result.Summary, Does.Contain($"Only {result.Placed} of 100 fit; held back by the spacing"));
            Assert.That(result.HeldBack, Is.Not.Empty);
        }

        [Test]
        public void Scatter_KeepsClearOfWhatAlreadyStands()
        {
            var world = BuildingFixture.Create();
            new PlaceObjectsCommand(world.Context).Execute(new PlaceObjectsCommand.Arguments
            {
                Objects = new[] { new PlaceObjectsCommand.Item { Object = BuildingFixture.House.ToString(), X = 64f, Z = 64f, Heading = 0f } }.ToList(),
            });

            Scatter(world, 40, size: 20f, spacing: 3f);

            var trees = world.Session.World.Records.OfType<PlacedObjectRecord>().Where(p => p.BaseId == BuildingFixture.Tree)
                .Select(p => new Vector2(p.Position.X, p.Position.Z));
            Assert.That(trees, Has.All.Matches<Vector2>(p => Vector2.Distance(p, new Vector2(64f, 64f)) >= 3f));
        }

        [Test]
        public void Scatter_MixesItsKinds_AndClustersWithClearings()
        {
            var world = BuildingFixture.Create();

            var result = Scatter(world, 40, size: 100f, clustering: 1f, kinds: new[] { BuildingFixture.Tree, BuildingFixture.House });

            var kinds = world.Session.World.Records.OfType<PlacedObjectRecord>().Select(p => p.BaseId).Distinct();
            Assert.That(kinds, Is.EquivalentTo(new[] { BuildingFixture.Tree, BuildingFixture.House }));
            Assert.That(result.Placed, Is.EqualTo(40));
        }

        [Test]
        public void Scatter_UnknownKinds_PlaceNothing()
        {
            var world = BuildingFixture.Create();

            Assert.That(() => new ScatterObjectsCommand(world.Context).Execute(new ScatterObjectsCommand.Arguments
                {
                    Objects = new[] { "core:dragon" }.ToList(), Count = 5, X = 64f, Z = 64f, Width = 20f, Depth = 20f,
                }),
                Throws.TypeOf<CommandException>().With.Message.Contains("no object 'core:dragon'"));
            Assert.That(world.Session.CanUndo, Is.False);
        }

        [Test]
        public void GroundAt_GivesHeightAndSlope_AndSaysWhereThereIsNoLand()
        {
            var result = new GroundAtCommand(BuildingFixture.Create().Context).Execute(new GroundAtCommand.Arguments
            {
                Points = new[] { new GroundPoint { X = 30f, Z = 40f }, new GroundPoint { X = 500f, Z = 500f } }.ToList(),
            });

            Assert.That(result.Points[0].Height, Is.EqualTo(13f));
            Assert.That(result.Points[0].Slope, Is.EqualTo(6f), "atan(0.1) is 5.7°.");
            Assert.That(result.Points[0].WaterDepth, Is.Null);
            Assert.That(result.Points[1].Height, Is.Null);
            Assert.That(result.Summary, Is.EqualTo("Read the ground at 2 point(s); 1 have no land."));
        }
    }
}
