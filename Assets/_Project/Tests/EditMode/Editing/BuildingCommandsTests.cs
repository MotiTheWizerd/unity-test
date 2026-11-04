using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Placement;
using Forge.Tests.Data;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class BuildingCommandsTests
    {
        static PlaceObjectsCommand.Item Item(RecordId id, float x, float z, float? heading = null, float? scale = null) =>
            new PlaceObjectsCommand.Item { Object = id.ToString(), X = x, Z = z, Heading = heading, Scale = scale };

        static PlaceObjectsCommand.Result Place(BuildingFixture world, params PlaceObjectsCommand.Item[] items) =>
            new PlaceObjectsCommand(world.Context).Execute(new PlaceObjectsCommand.Arguments { Objects = items.ToList() });

        static PlaceLineCommand.Result Line(BuildingFixture world, RecordId id, bool closed, float? gap, params (float X, float Z)[] points) =>
            new PlaceLineCommand(world.Context).Execute(new PlaceLineCommand.Arguments
            {
                Object = id.ToString(),
                Points = points.Select(p => new GroundPoint { X = p.X, Z = p.Z }).ToList(),
                Closed = closed,
                Gap = gap,
            });

        static List<PlacedObjectRecord> Placed(BuildingFixture world) =>
            world.Session.World.Records.OfType<PlacedObjectRecord>().ToList();

        static float AngleBetween(float a, float b) => Math.Abs(((a - b) % 360f + 540f) % 360f - 180f);

        [Test]
        public void ListObjects_ReportsSizesAndHowEachIsPlaced()
        {
            var result = new ListObjectsCommand(BuildingFixture.Create().Context).Execute(new ListObjectsCommand.Arguments());

            Assert.That(result.Objects.Select(o => o.Name), Is.EquivalentTo(new[] { "Pine Tree", "Wooden Fence", "Small House" }));
            var fence = result.Objects.Single(o => o.Id == "core:fence");
            Assert.That((fence.Width, fence.Depth), Is.EqualTo((3f, 0.2f)));
            Assert.That(fence.TiltsToSlope, Is.True);
            var tree = result.Objects.Single(o => o.Id == "core:tree");
            Assert.That((tree.RandomHeading, tree.MinScale, tree.MaxScale), Is.EqualTo((true, 0.8f, 1.2f)));
        }

        [Test]
        public void ListObjects_Search_Narrows()
        {
            var result = new ListObjectsCommand(BuildingFixture.Create().Context).Execute(new ListObjectsCommand.Arguments { Search = "tree" });

            Assert.That(result.Objects.Select(o => o.Id), Is.EqualTo(new[] { "core:tree" }));
            Assert.That(result.Summary, Does.Contain("1 of 3"));
        }

        [Test]
        public void InspectArea_SamplesTheGroundOnAGrid()
        {
            var result = new InspectAreaCommand(BuildingFixture.Create().Context)
                .Execute(new InspectAreaCommand.Arguments { X = 50f, Z = 50f, Size = 20f, Samples = 5 });

            var terrain = result.Terrain;
            Assert.That(terrain.Heights.Count, Is.EqualTo(5));
            Assert.That(terrain.Heights.All(row => row.Count == 5), Is.True);
            Assert.That(terrain.Spacing, Is.EqualTo(5f));
            Assert.That((terrain.MinHeight, terrain.MaxHeight), Is.EqualTo((14f, 16f)));
            Assert.That(terrain.Heights[0][0], Is.EqualTo(14f), "Row 0 is the south edge, column 0 the west edge.");
            Assert.That(terrain.Heights[0][4], Is.EqualTo(16f));
            Assert.That(terrain.SteepestSlope, Is.EqualTo(6f), "A rise of 1 in 10 is about 5.7°.");
        }

        [Test]
        public void InspectArea_ACoarseGrid_StillReadsEveryTerrainSample()
        {
            // The whole land at 2 × 2 probes only the far corners, which are all off the terrain.
            var result = new InspectAreaCommand(BuildingFixture.Create().Context)
                .Execute(new InspectAreaCommand.Arguments { X = 64f, Z = 64f, Size = 256f, Samples = 2 });

            Assert.That(result.Terrain, Is.Not.Null);
            Assert.That(result.Terrain.Heights.SelectMany(row => row).All(height => height == null), Is.True);
            Assert.That((result.Terrain.MinHeight, result.Terrain.MaxHeight), Is.EqualTo((10f, 22.7f)));
            Assert.That(result.Summary, Does.Contain("ground 10–22.7 m"));
        }

        [Test]
        public void InspectArea_ListsTheObjectsInside_NearestFirst()
        {
            var world = BuildingFixture.Create();
            Place(world, Item(BuildingFixture.House, 58f, 50f), Item(BuildingFixture.House, 51f, 50f), Item(BuildingFixture.House, 90f, 90f));

            var result = new InspectAreaCommand(world.Context).Execute(new InspectAreaCommand.Arguments { X = 50f, Z = 50f, Size = 20f });

            Assert.That(result.ObjectCount, Is.EqualTo(2));
            Assert.That(result.Objects.Select(o => o.X), Is.EqualTo(new[] { 51f, 58f }));
            Assert.That(result.Objects[0].Name, Is.EqualTo("Small House"));
        }

        [Test]
        public void InspectArea_OffTheTerrain_SaysSo()
        {
            var result = new InspectAreaCommand(BuildingFixture.Create().Context)
                .Execute(new InspectAreaCommand.Arguments { X = 500f, Z = 500f, Size = 10f });

            Assert.That(result.Terrain, Is.Null);
            Assert.That(result.Summary, Does.Contain("no terrain"));
        }

        [Test]
        public void PlaceObjects_SeatsEachOnTheGround_AsOneEdit()
        {
            var world = BuildingFixture.Create();

            var result = Place(world, Item(BuildingFixture.House, 20f, 30f, heading: 90f), Item(BuildingFixture.Tree, 40f, 40f));

            var house = result.Placed[0];
            Assert.That((house.X, house.Y, house.Z, house.Heading, house.Scale), Is.EqualTo((20f, 12f, 30f, 90f, 1f)));
            Assert.That(result.Placed[1].Scale, Is.InRange(0.8f, 1.2f), "Left out, the tree's size comes from its rules.");
            Assert.That(world.Session.UndoDescription, Is.EqualTo("Place 2 objects"));

            world.Session.Undo();
            Assert.That(Placed(world), Is.Empty);
        }

        [Test]
        public void PlaceObjects_TiltsSlopeObjects_SoBothEndsTouchTheGround()
        {
            var world = BuildingFixture.Create();

            Place(world, Item(BuildingFixture.Fence, 50f, 50f, heading: 0f));

            var fence = Placed(world).Single();
            foreach (var end in new[] { 1.5f, -1.5f })
            {
                var point = fence.Position + Vector3.Transform(new Vector3(end, 0f, 0f), fence.Rotation);
                Assert.That(point.Y, Is.EqualTo(BuildingFixture.Ground(new Vector2(point.X, point.Z)).Value).Within(0.02f), $"end {end}");
            }
        }

        [Test]
        public void PlaceObjects_SkipsPositionsWithoutTerrain()
        {
            var world = BuildingFixture.Create();

            var result = Place(world, Item(BuildingFixture.House, 20f, 20f), Item(BuildingFixture.House, -50f, 20f));

            Assert.That(result.Placed.Count, Is.EqualTo(1));
            Assert.That(result.Skipped.Single().Index, Is.EqualTo(1));
            Assert.That(result.Summary, Does.Contain("Skipped 1"));
        }

        [Test]
        public void PlaceObjects_AnUnknownObject_PlacesNothing()
        {
            var world = BuildingFixture.Create();

            var error = Assert.Throws<CommandException>(() =>
                Place(world, Item(BuildingFixture.House, 20f, 20f), new PlaceObjectsCommand.Item { Object = "core:dragon", X = 1f, Z = 1f }));

            Assert.That(error.Message, Does.Contain("core:dragon").And.Contain("list_objects"));
            Assert.That(Placed(world), Is.Empty);
        }

        [Test]
        public void PlaceObjects_WhereTheSceneHasNotLoadedTheTerrain_SaysSo()
        {
            var world = BuildingFixture.Create();
            var nothingLoaded = new ObjectSeating(p => null, id => null, key => BuildingFixture.HouseShape);
            var command = new PlaceObjectsCommand(new BuildContext(world.Session, () => BuildingFixture.Space, nothingLoaded, world.Options));

            var error = Assert.Throws<CommandException>(() => command.Execute(new PlaceObjectsCommand.Arguments
            {
                Objects = new List<PlaceObjectsCommand.Item> { Item(BuildingFixture.House, 20f, 20f) },
            }));

            Assert.That(error.Message, Does.Contain("isn't loaded"));
        }

        [Test]
        public void PlaceObjects_NothingOnTheTerrain_IsRefused()
        {
            var world = BuildingFixture.Create();

            Assert.Throws<CommandException>(() => Place(world, Item(BuildingFixture.House, -5f, -5f)));
            Assert.That(world.Session.CanUndo, Is.False);
        }

        [Test]
        public void PlaceLine_LaysFencesEndToEnd_AlongTheStretch()
        {
            var world = BuildingFixture.Create();

            var result = Line(world, BuildingFixture.Fence, false, null, (10f, 10f), (19.3f, 10f));

            var fences = Placed(world).OrderBy(p => p.Position.X).ToList();
            Assert.That(fences.Select(f => (float)Math.Round(f.Position.X, 2)), Is.EqualTo(new[] { 10f, 13f, 16f, 19f }));
            Assert.That(fences.All(f => AngleBetween(PlacementMath.HeadingOf(f.Rotation), 0f) < 1f), Is.True, "Their length runs along the line.");
            Assert.That(result.Ids.Count, Is.EqualTo(4));
            Assert.That(world.Session.UndoDescription, Is.EqualTo("Place 4 Wooden Fence"));
        }

        [Test]
        public void PlaceLine_ClosedLoop_PutsOneObjectOnEachCorner()
        {
            var world = BuildingFixture.Create();

            Line(world, BuildingFixture.Fence, true, null, (10f, 10f), (19f, 10f), (19f, 19f), (10f, 19f));

            var spots = Placed(world).Select(p => new Vector2(p.Position.X, p.Position.Z)).ToList();
            Assert.That(spots.Count, Is.EqualTo(12), "Four 9 m sides of 3 m fences, corners shared.");
            for (var i = 0; i < spots.Count; i++)
            for (var j = i + 1; j < spots.Count; j++)
                Assert.That(Vector2.Distance(spots[i], spots[j]), Is.GreaterThan(1f));
        }

        [Test]
        public void PlaceLine_UsesTheGivenGap()
        {
            var world = BuildingFixture.Create();

            Line(world, BuildingFixture.Fence, false, 1f, (10f, 10f), (22f, 10f));

            Assert.That(Placed(world).Select(p => (float)Math.Round(p.Position.X, 2)).OrderBy(x => x), Is.EqualTo(new[] { 10f, 14f, 18f, 22f }));
        }

        [Test]
        public void PlaceLine_Trees_KeepTheirOwnHeadings()
        {
            var world = BuildingFixture.Create();

            Line(world, BuildingFixture.Tree, false, null, (10f, 50f), (90f, 50f));

            var headings = Placed(world).Select(p => Math.Round(PlacementMath.HeadingOf(p.Rotation))).Distinct().Count();
            Assert.That(headings, Is.GreaterThan(3), "Natural things don't line up with the path.");
        }

        [Test]
        public void PlaceLine_NeedingTooManyObjects_IsRefused()
        {
            var world = BuildingFixture.Create();

            var error = Assert.Throws<CommandException>(() => Line(world, BuildingFixture.Fence, false, null, (0f, 0f), (4000f, 0f)));

            Assert.That(error.Message, Does.Contain(PlaceLineCommand.MaxObjects.ToString()));
            Assert.That(Placed(world), Is.Empty);
        }

        [Test]
        public void DeleteObjects_RemovesThem_AndNamesTheMissing()
        {
            var world = BuildingFixture.Create();
            var placed = Place(world, Item(BuildingFixture.House, 20f, 20f), Item(BuildingFixture.House, 40f, 20f));

            var result = new DeleteObjectsCommand(world.Context).Execute(new DeleteObjectsCommand.Arguments
            {
                Ids = new List<string> { placed.Placed[0].Id, "user:ref/999999" },
            });

            Assert.That(result.Removed, Is.EqualTo(1));
            Assert.That(result.Missing, Is.EqualTo(new[] { "user:ref/999999" }));
            Assert.That(Placed(world).Select(p => p.Id.ToString()), Is.EqualTo(new[] { placed.Placed[1].Id }));
        }

        [Test]
        public void DeleteObjects_NothingFound_IsRefused()
        {
            var world = BuildingFixture.Create();

            Assert.Throws<CommandException>(() => new DeleteObjectsCommand(world.Context)
                .Execute(new DeleteObjectsCommand.Arguments { Ids = new List<string> { "core:tree" } }));
        }

        static readonly RecordId ElsewhereId = RecordId.Parse("core:ref/elsewhere");

        // A house standing in another space, at a spot that also has ground in the fixture's space.
        static PlacedObjectRecord Elsewhere(float x, float z) =>
            new PlacedObjectRecord(ElsewhereId, BuildingFixture.House, TestRecords.OtherSpace, new Vector3(x, 15f, z), Quaternion.Identity, 1f);

        [Test]
        public void PlaceObjects_PutsThemInTheSpaceBeingBuiltIn()
        {
            var world = BuildingFixture.Create();

            Place(world, Item(BuildingFixture.House, 20f, 20f));

            Assert.That(Placed(world).Single().Space, Is.EqualTo(BuildingFixture.Space));
        }

        [Test]
        public void DeleteObjects_AnObjectInAnotherSpace_IsNotFound()
        {
            var world = BuildingFixture.Create(Elsewhere(20f, 20f));
            var placed = Place(world, Item(BuildingFixture.House, 40f, 20f));

            var result = new DeleteObjectsCommand(world.Context).Execute(new DeleteObjectsCommand.Arguments
            {
                Ids = new List<string> { placed.Placed[0].Id, ElsewhereId.ToString() },
            });

            Assert.That(result.Removed, Is.EqualTo(1));
            Assert.That(result.Missing, Is.EqualTo(new[] { ElsewhereId.ToString() }));
            Assert.That(world.Session.World.Records.Contains(ElsewhereId), Is.True);
        }

        [Test]
        public void DeleteObjects_OnlyObjectsInAnotherSpace_IsRefused()
        {
            var world = BuildingFixture.Create(Elsewhere(20f, 20f));

            Assert.Throws<CommandException>(() => new DeleteObjectsCommand(world.Context)
                .Execute(new DeleteObjectsCommand.Arguments { Ids = new List<string> { ElsewhereId.ToString() } }));
            Assert.That(world.Session.World.Records.Contains(ElsewhereId), Is.True);
            Assert.That(world.Session.CanUndo, Is.False);
        }

        [Test]
        public void AdjustObjects_AnObjectInAnotherSpace_IsSkippedAsNotFound()
        {
            var world = BuildingFixture.Create(Elsewhere(20f, 20f));
            var house = Place(world, Item(BuildingFixture.House, 40f, 20f)).Placed[0];

            var result = new AdjustObjectsCommand(world.Context).Execute(new AdjustObjectsCommand.Arguments
            {
                Objects = new List<AdjustObjectsCommand.Item>
                {
                    new AdjustObjectsCommand.Item { Id = house.Id, X = 50f },
                    new AdjustObjectsCommand.Item { Id = ElsewhereId.ToString(), X = 30f },
                },
            });

            Assert.That(result.Adjusted.Select(o => o.Id), Is.EqualTo(new[] { house.Id }));
            Assert.That(result.Skipped.Single().Id, Is.EqualTo(ElsewhereId.ToString()));
            Assert.That(result.Skipped[0].Reason, Does.Contain("no placed object"));
            Assert.That(world.Session.World.Records.Get<PlacedObjectRecord>(ElsewhereId).Position.X, Is.EqualTo(20f));
        }

        [Test]
        public void AdjustObjects_OnlyObjectsInAnotherSpace_IsRefused()
        {
            var world = BuildingFixture.Create(Elsewhere(20f, 20f));

            Assert.Throws<CommandException>(() => new AdjustObjectsCommand(world.Context).Execute(new AdjustObjectsCommand.Arguments
            {
                Objects = new List<AdjustObjectsCommand.Item> { new AdjustObjectsCommand.Item { Id = ElsewhereId.ToString(), X = 30f } },
            }));
            Assert.That(world.Session.CanUndo, Is.False);
        }

        [Test]
        public void InspectArea_LeavesOutObjectsInAnotherSpace()
        {
            var world = BuildingFixture.Create(Elsewhere(50f, 50f));
            Place(world, Item(BuildingFixture.House, 51f, 50f));

            var result = new InspectAreaCommand(world.Context).Execute(new InspectAreaCommand.Arguments { X = 50f, Z = 50f, Size = 20f });

            Assert.That(result.ObjectCount, Is.EqualTo(1));
            Assert.That(result.Objects.Select(o => o.X), Is.EqualTo(new[] { 51f }));
        }

        [Test]
        public void BuildContext_FollowsTheSpaceItIsGiven()
        {
            var world = BuildingFixture.Create(Elsewhere(20f, 20f));
            var space = BuildingFixture.Space;
            var context = new BuildContext(world.Session, () => space, world.Context.Seating, world.Options);
            Assert.That(context.PlacedInSpace(ElsewhereId.ToString()), Is.Null);
            Assert.That(context.Terrain.Contains(new ChunkCoord(0, 0)), Is.True);

            space = TestRecords.OtherSpace;

            Assert.That(context.Space, Is.EqualTo(TestRecords.OtherSpace));
            Assert.That(context.PlacedInSpace(ElsewhereId.ToString()).Id, Is.EqualTo(ElsewhereId));
            Assert.That(context.PlacedObjects().Select(p => p.Id), Is.EqualTo(new[] { ElsewhereId }));
            Assert.That(context.Terrain.Contains(new ChunkCoord(0, 0)), Is.False, "The other space has no land.");
        }
    }
}
