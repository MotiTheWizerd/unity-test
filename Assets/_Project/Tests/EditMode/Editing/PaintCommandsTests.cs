using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Terrain;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class PaintCommandsTests
    {
        static readonly RecordId Grass = GroundLayerRecord.IdFor("core", "grass");
        static readonly RecordId Dirt = GroundLayerRecord.IdFor("core", "dirt");
        static readonly RecordId Gravel = GroundLayerRecord.IdFor("core", "gravel");

        // The fixture's land with three grounds; grass shows the base look, so it is the unpainted ground.
        static BuildingFixture NewWorld() => BuildingFixture.Create(
            new GroundLayerRecord(Grass, "Grass", BuildingFixture.BaseGroundLook),
            new GroundLayerRecord(Dirt, "Dirt", "ground/dirt"),
            new GroundLayerRecord(Gravel, "Gravel", "ground/gravel"));

        static GroundPoint Point(float x, float z) => new GroundPoint { X = x, Z = z };

        static int Share(BuildingFixture world, RecordId layer, float x, float z)
        {
            var coord = new ChunkCoord((int)(x / 64f), (int)(z / 64f));
            var terrain = new TerrainGrid(world.Session.World.Records, BuildingFixture.Space);
            var paint = terrain.TryGetChunk(coord, out var chunk) ? chunk.Paint?.Load() : null;
            var index = paint?.IndexOf(layer) ?? -1;
            return index < 0 ? 0 : paint[index, (int)x - coord.X * 64, (int)z - coord.Z * 64];
        }

        [Test]
        public void ListGrounds_NamesThem_AndWhichOneIsUnpainted()
        {
            var result = new ListGroundsCommand(NewWorld().Context).Execute(new ListGroundsCommand.Arguments());

            Assert.That(result.Grounds.Select(g => g.Name), Is.EqualTo(new[] { "Dirt", "Grass", "Gravel" }));
            Assert.That(result.Grounds.Single(g => g.Unpainted).Id, Is.EqualTo(Grass.ToString()));
        }

        [Test]
        public void PaintLine_PaintsTheGroundByName_AsOneUndoStep()
        {
            var world = NewWorld();

            var result = new PaintLineCommand(world.Context).Execute(new PaintLineCommand.Arguments
            {
                Ground = "dirt",
                Points = new[] { Point(10f, 20.5f), Point(40f, 20.5f) }.ToList(),
                Width = 4f,
            });

            Assert.That(result.Summary, Does.StartWith("Painted Dirt along 30 m, 4 m wide"));
            Assert.That(result.Length, Is.EqualTo(30f));
            Assert.That(result.Area, Is.GreaterThan(120));
            Assert.That(Share(world, Dirt, 25f, 20f), Is.EqualTo(GroundPaint.Full));
            Assert.That(Share(world, Grass, 25f, 30f), Is.EqualTo(GroundPaint.Full), "Unpainted ground beside the path counts as grass.");
            Assert.That(world.Session.UndoDescription, Is.EqualTo("Paint (Dirt)"));
        }

        [Test]
        public void PaintArea_TakesARectangle_OrPoints()
        {
            var world = NewWorld();
            var area = new PaintAreaCommand(world.Context);

            var yard = area.Execute(new PaintAreaCommand.Arguments { Ground = Gravel.ToString(), X = 30f, Z = 30f, Width = 8f, Depth = 6f, Heading = 0f });
            var field = area.Execute(new PaintAreaCommand.Arguments
            {
                Ground = "Dirt",
                Points = new[] { Point(60f, 60f), Point(90f, 60f), Point(75f, 90f) }.ToList(),
                Edge = 0f,
            });

            Assert.That(yard.Summary, Does.StartWith("Painted Gravel over a 8 × 6 m area around (30, 30)"));
            Assert.That(Share(world, Gravel, 30f, 30f), Is.EqualTo(GroundPaint.Full));
            Assert.That(field.Summary, Does.Contain("3-cornered area of about 450 m²"));
            Assert.That(Share(world, Dirt, 75f, 70f), Is.EqualTo(GroundPaint.Full));
            Assert.That(Share(world, Dirt, 62f, 88f), Is.Zero, "Outside the triangle.");
        }

        [Test]
        public void PaintArea_NeedsExactlyOneShape()
        {
            var area = new PaintAreaCommand(NewWorld().Context);

            Assert.That(() => area.Execute(new PaintAreaCommand.Arguments { Ground = "Dirt" }),
                Throws.TypeOf<CommandException>().With.Message.Contains("either a rectangle"));
            Assert.That(() => area.Execute(new PaintAreaCommand.Arguments
                {
                    Ground = "Dirt", X = 10f, Z = 10f, Width = 4f, Depth = 4f,
                    Points = new[] { Point(1f, 1f), Point(5f, 1f), Point(1f, 5f) }.ToList(),
                }),
                Throws.TypeOf<CommandException>().With.Message.Contains("not both"));
            Assert.That(() => area.Execute(new PaintAreaCommand.Arguments { Ground = "Dirt", X = 10f, Z = 10f }),
                Throws.TypeOf<CommandException>().With.Message.Contains("needs x, z, width and depth"));
        }

        [Test]
        public void Painting_IsRefused_WithWhatWouldWork()
        {
            var line = new PaintLineCommand(NewWorld().Context);
            PaintLineCommand.Arguments Along(string ground, float x) =>
                new PaintLineCommand.Arguments { Ground = ground, Points = new[] { Point(x, 10f), Point(x + 10f, 10f) }.ToList() };

            Assert.That(() => line.Execute(Along("lava", 10f)),
                Throws.TypeOf<CommandException>().With.Message.EqualTo("There is no ground 'lava'. The grounds are: Dirt, Grass, Gravel."));
            Assert.That(() => line.Execute(Along("Dirt", 500f)),
                Throws.TypeOf<CommandException>().With.Message.Contains("no land under that shape"));

            // A world without a ground for unpainted land: painting would change how all of it looks.
            var bare = new PaintLineCommand(BuildingFixture.Create(new GroundLayerRecord(Dirt, "Dirt", "ground/dirt")).Context);
            Assert.That(() => bare.Execute(Along("Dirt", 10f)),
                Throws.TypeOf<CommandException>().With.Message.Contains("no ground layer for unpainted land"));
        }

        [Test]
        public void Painting_GoesIntoAnOpenDraft_LikeEveryBuild()
        {
            var world = NewWorld();
            var draft = world.Session.BeginDraft();

            new PaintAreaCommand(world.Context).Execute(new PaintAreaCommand.Arguments { Ground = "Dirt", X = 20f, Z = 20f, Width = 6f, Depth = 6f });

            Assert.That(draft.HasChanges, Is.True);
            draft.Discard();
            Assert.That(Share(world, Dirt, 20f, 20f), Is.Zero, "Discarding the draft takes the paint away.");
        }
    }
}
