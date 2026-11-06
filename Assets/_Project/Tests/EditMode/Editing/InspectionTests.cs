using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Inspection;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class InspectionTests
    {
        static PlacedObjectRecord[] PlaceTwo(BuildingFixture world, float scaleA, float scaleB)
        {
            var placed = new PlaceObjectsCommand(world.Context).Execute(new PlaceObjectsCommand.Arguments
            {
                Objects = new[]
                {
                    new PlaceObjectsCommand.Item { Object = BuildingFixture.House.ToString(), X = 20f, Z = 20f, Scale = scaleA },
                    new PlaceObjectsCommand.Item { Object = BuildingFixture.House.ToString(), X = 40f, Z = 20f, Scale = scaleB },
                }.ToList(),
            }).Placed;
            return placed.Select(p => world.Session.World.Records.Get<PlacedObjectRecord>(RecordId.Parse(p.Id))).ToArray();
        }

        [Test]
        public void Of_TakesTheSelectedRecordsInOrder_AndSkipsIdsThatAreGone()
        {
            var world = BuildingFixture.Create();
            var placed = PlaceTwo(world, 1f, 1f);

            var inspection = Inspection.Of(world.Session.World.Records, new[] { placed[1].Id, new RecordId("test", "gone"), placed[0].Id });

            Assert.That(inspection.Records.Select(r => r.Id), Is.EqualTo(new[] { placed[1].Id, placed[0].Id }));
            Assert.That(inspection.AllAre<PlacedObjectRecord>(), Is.True);
            Assert.That(inspection.All<PlacedObjectRecord>(), Has.Count.EqualTo(2));
            Assert.That(inspection.Find<BaseObjectRecord>(placed[0].BaseId), Is.Not.Null);
        }

        [Test]
        public void Empty_InspectsNothing()
        {
            Assert.That(Inspection.Empty.IsEmpty, Is.True);
            Assert.That(Inspection.Empty.AllAre<PlacedObjectRecord>(), Is.False);
            Assert.That(Inspection.Empty.Find<BaseObjectRecord>(BuildingFixture.House), Is.Null);
        }

        [Test]
        public void Shared_GivesTheValueTheyAgreeOn_OrSaysMixed()
        {
            var world = BuildingFixture.Create();
            var placed = PlaceTwo(world, 2f, 2f);

            var scale = Inspection.Shared(placed, r => r.Scale);
            var x = Inspection.Shared(placed, r => r.Position.X);

            Assert.That(scale.Any && !scale.Mixed && scale.Value == 2f, Is.True);
            Assert.That(x.Mixed, Is.True);
            Assert.That(x.Value, Is.EqualTo(placed[0].Position.X), "Mixed keeps the first record's value.");
        }

        [Test]
        public void Shared_UsesTheGivenSameness_AndIsNoneForNoRecords()
        {
            var world = BuildingFixture.Create();
            var placed = PlaceTwo(world, 1f, 1.0004f);

            var scale = Inspection.Shared(placed, r => r.Scale, (p, q) => Math.Abs(p - q) < 0.001f);

            Assert.That(scale.Mixed, Is.False);
            Assert.That(Inspection.Shared(Array.Empty<PlacedObjectRecord>(), r => r.Scale).Any, Is.False);
        }
    }
}
