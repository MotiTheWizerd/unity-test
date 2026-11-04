using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Placement;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>place_outline on the fixture's terrain (fence 3 × 0.2 m, house 4 × 5 m).</summary>
    public class PlaceOutlineTests
    {
        static PlaceOutlineCommand.Result Run(BuildingFixture world, PlaceOutlineCommand.Arguments arguments) =>
            new PlaceOutlineCommand(world.Context).Execute(arguments);

        static PlacedObjectRecord Placed(BuildingFixture world, string id) =>
            world.Session.World.Records.Get<PlacedObjectRecord>(RecordId.Parse(id));

        [Test]
        public void Outline_EnclosesTheRectangle_AsOneUndoStep()
        {
            var world = BuildingFixture.Create();

            // Each side is the size minus the 0.2 m thickness: 12 m (4 fences) back and front, 6 m (2 fences) the sides.
            var result = Run(world, new PlaceOutlineCommand.Arguments
            {
                Object = BuildingFixture.Fence.ToString(), X = 30f, Z = 30f, Width = 12.2f, Depth = 6.2f,
            });

            Assert.That(result.Ids.Count, Is.EqualTo(12));
            Assert.That(result.Summary, Does.StartWith("Placed 12 Wooden Fence around a 12.2 × 6.2 m rectangle"));
            Assert.That(world.Session.UndoDescription, Does.StartWith("Place 12 Wooden Fence around"));
            // Fences run along their side: the back ones east-west, the side ones north-south.
            var backHeading = PlacementMath.HeadingOf(Placed(world, result.Ids[0]).Rotation);
            Assert.That(System.Math.Min(backHeading, 360f - backHeading), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(PlacementMath.HeadingOf(Placed(world, result.Ids[4]).Rotation) % 180f, Is.EqualTo(90f).Within(1e-3f));

            world.Session.Undo();
            Assert.That(world.Session.World.Records.TryGet(RecordId.Parse(result.Ids[0]), out _), Is.False);
        }

        [Test]
        public void Fill_CoversTheRectangle()
        {
            var world = BuildingFixture.Create();

            var result = Run(world, new PlaceOutlineCommand.Arguments
            {
                Object = BuildingFixture.House.ToString(), X = 40f, Z = 40f, Width = 8f, Depth = 10f, Fill = true,
            });

            Assert.That(result.Ids.Count, Is.EqualTo(4));
            Assert.That(result.Summary, Does.StartWith("Placed 4 Small House over a 8 × 10 m rectangle"));
        }

        [Test]
        public void PiecesWithoutGround_AreSkipped_TheRestIsPlaced()
        {
            var world = BuildingFixture.Create();

            // Two houses, at x = 124 and 128: the second is past the terrain's east edge (x = 127).
            var result = Run(world, new PlaceOutlineCommand.Arguments
            {
                Object = BuildingFixture.House.ToString(), X = 126f, Z = 40f, Width = 8f, Depth = 5f, Fill = true,
            });

            Assert.That(result.Ids.Count, Is.EqualTo(1));
            Assert.That(result.Summary, Does.Contain("Skipped 1"));
        }

        [Test]
        public void TooManyPieces_AreRefused_AndNothingChanges()
        {
            var world = BuildingFixture.Create();

            var error = Assert.Throws<CommandException>(() => Run(world, new PlaceOutlineCommand.Arguments
            {
                Object = BuildingFixture.Fence.ToString(), X = 60f, Z = 60f, Width = 100f, Depth = 100f, Fill = true,
            }));

            Assert.That(error.Message, Does.Contain("more than"));
            Assert.That(world.Session.CanUndo, Is.False);
        }
    }
}
