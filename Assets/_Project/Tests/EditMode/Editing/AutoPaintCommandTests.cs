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
    /// <summary>auto_paint on the fixture's land, a gentle slope (under 6°) from 10 m to 23 m, with a mountain sculpted on it.</summary>
    public class AutoPaintCommandTests
    {
        static readonly RecordId Grass = GroundLayerRecord.IdFor("core", "grass");
        static readonly RecordId Dirt = GroundLayerRecord.IdFor("core", "dirt");
        static readonly RecordId Rock = GroundLayerRecord.IdFor("core", "rock");
        static readonly RecordId Snow = GroundLayerRecord.IdFor("core", "snow");
        static readonly RecordId Gravel = GroundLayerRecord.IdFor("core", "gravel");

        static BuildingFixture NewWorld(bool withSnow = true)
        {
            var grounds = new[]
            {
                new GroundLayerRecord(Grass, "Grass", BuildingFixture.BaseGroundLook),
                new GroundLayerRecord(Dirt, "Dirt", "ground/dirt"),
                new GroundLayerRecord(Rock, "Rock", "ground/rock"),
                new GroundLayerRecord(Gravel, "Gravel", "ground/gravel"),
                new GroundLayerRecord(Snow, "Snow", "ground/snow"),
            };
            return BuildingFixture.Create(withSnow ? grounds : grounds.Take(4).ToArray());
        }

        // A round mountain 50 m high and 120 m across in the middle of the land: a level top at 66.4 m, sides up to 51° steep.
        static void Mountain(BuildingFixture world) =>
            new SculptHillCommand(world.Context).Execute(new SculptHillCommand.Arguments
            {
                X = 64f, Z = 64f, Width = 120f, Height = 50f, Roughness = 0f,
            });

        static RecordId DominantAt(BuildingFixture world, int x, int z)
        {
            var coord = new ChunkCoord(x / 64, z / 64);
            var terrain = new TerrainGrid(world.Session.World.Records, BuildingFixture.Space);
            var paint = terrain.TryGetChunk(coord, out var chunk) ? chunk.Paint?.Load() : null;
            return paint?.DominantAt(x - coord.X * 64, z - coord.Z * 64) ?? Grass;
        }

        [Test]
        public void AutoPaint_PaintsAMountainByItsShape_WithASnowLine()
        {
            var world = NewWorld();
            Mountain(world);

            var result = new AutoPaintCommand(world.Context).Execute(new AutoPaintCommand.Arguments
            {
                X = 64f, Z = 64f, Width = 120f, Depth = 120f, SnowLine = 52f,
            });

            Assert.That(DominantAt(world, 64, 64), Is.EqualTo(Snow), "The top, at 66 m.");
            Assert.That(DominantAt(world, 64, 29), Is.EqualTo(Rock), "A 50° side at 35 m, below the snow.");
            Assert.That(DominantAt(world, 10, 10), Is.EqualTo(Grass), "Gentle land off the mountain.");
            Assert.That(result.Summary, Does.StartWith("Auto painted a 120 × 120 m area around (64, 64), heading 0° by the land's shape, snow from 52 m: "));
            Assert.That(result.Grounds.Select(g => g.Name), Is.SupersetOf(new[] { "Snow", "Rock", "Grass" }));
            Assert.That(result.Grounds.Sum(g => g.Percent), Is.InRange(98, 102));
            Assert.That(world.Session.UndoDescription, Is.EqualTo("Auto paint"));
        }

        [Test]
        public void SnowHoldsTo90_CoversTheSteepFaces_Too()
        {
            var world = NewWorld();
            Mountain(world);

            new AutoPaintCommand(world.Context).Execute(new AutoPaintCommand.Arguments
            {
                X = 64f, Z = 64f, Width = 120f, Depth = 120f, SnowLine = 20f, SnowHoldsTo = 90f,
            });

            Assert.That(DominantAt(world, 64, 29), Is.EqualTo(Snow), "The 50° side at 35 m is snow now.");
        }

        [Test]
        public void AutoPaint_RepaintsInsideItsArea_AndKeepsThePaintOutside()
        {
            var world = NewWorld();
            var paint = new PaintAreaCommand(world.Context);
            paint.Execute(new PaintAreaCommand.Arguments { Ground = "Dirt", X = 30f, Z = 64f, Width = 10f, Depth = 10f });
            paint.Execute(new PaintAreaCommand.Arguments { Ground = "Dirt", X = 100f, Z = 64f, Width = 10f, Depth = 10f });

            new AutoPaintCommand(world.Context).Execute(new AutoPaintCommand.Arguments { X = 30f, Z = 64f, Width = 40f, Depth = 40f });

            Assert.That(DominantAt(world, 30, 64), Is.EqualTo(Grass), "Inside, the gentle slope is flat land.");
            Assert.That(DominantAt(world, 100, 64), Is.EqualTo(Dirt), "Outside, the hand-painted dirt stays.");
        }

        [Test]
        public void AutoPaint_SwapsARolesGround()
        {
            var world = NewWorld();

            var result = new AutoPaintCommand(world.Context).Execute(new AutoPaintCommand.Arguments { X = 64f, Z = 64f, Width = 20f, Depth = 20f, Flat = "gravel" });

            Assert.That(DominantAt(world, 64, 64), Is.EqualTo(Gravel));
            Assert.That(result.Grounds.Select(g => g.Name), Is.EqualTo(new[] { "Gravel" }));
        }

        [Test]
        public void AutoPaint_IsRefused_WithWhatWouldWork()
        {
            var bare = new AutoPaintCommand(NewWorld(withSnow: false).Context);
            var auto = new AutoPaintCommand(NewWorld().Context);

            Assert.That(() => bare.Execute(new AutoPaintCommand.Arguments { X = 64f, Z = 64f, Width = 20f, Depth = 20f, SnowLine = 15f }),
                Throws.TypeOf<CommandException>().With.Message.Contains("no snow ground"));
            Assert.That(() => auto.Execute(new AutoPaintCommand.Arguments { X = 64f, Z = 64f, Width = 20f, Depth = 20f, Steep = "lava" }),
                Throws.TypeOf<CommandException>().With.Message.StartsWith("There is no ground 'lava'."));
            Assert.That(() => auto.Execute(new AutoPaintCommand.Arguments { X = 500f, Z = 500f, Width = 20f, Depth = 20f }),
                Throws.TypeOf<CommandException>().With.Message.Contains("no land under that shape"));
            Assert.That(() => auto.Execute(new AutoPaintCommand.Arguments { X = 64f, Z = 64f }),
                Throws.TypeOf<CommandException>().With.Message.Contains("needs x, z, width and depth"));
        }

        [Test]
        public void AutoPaint_TakesTheWidestArea_ItsSoftEdgeReachingPast()
        {
            var auto = new AutoPaintCommand(NewWorld().Context);

            Assert.That(() => auto.Execute(new AutoPaintCommand.Arguments { X = 64f, Z = 64f, Width = GroundPainting.MaxSpan, Depth = GroundPainting.MaxSpan }),
                Throws.Nothing);
        }

        [Test]
        public void AutoPaint_GoesIntoAnOpenDraft()
        {
            var world = NewWorld();
            var draft = world.Session.BeginDraft();

            new AutoPaintCommand(world.Context).Execute(new AutoPaintCommand.Arguments { X = 64f, Z = 64f, Width = 20f, Depth = 20f, Flat = "Gravel" });

            Assert.That(draft.HasChanges, Is.True);
            draft.Discard();
            Assert.That(DominantAt(world, 64, 64), Is.EqualTo(Grass));
        }
    }
}
