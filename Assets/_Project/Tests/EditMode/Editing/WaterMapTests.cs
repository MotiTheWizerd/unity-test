using System.Numerics;
using Forge.Data.Records;
using Forge.Editing.Terrain;
using Forge.Editing.Water;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class WaterMapTests
    {
        // The fixture's land rises from 10 m at x = 0 by 1 m per 10 m eastwards, so a sea at 15 m covers x < 50.
        static TerrainGrid Terrain() => new TerrainGrid(BuildingFixture.Create().Session.World.Records, BuildingFixture.Space);

        [Test]
        public void BelowTheSeaLevel_IsUnderWater_AsDeepAsTheGroundIsLow()
        {
            var water = new WaterMap(Terrain(), 15f);

            Assert.That(water.DepthAt(new Vector2(0f, 20f)), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(water.DepthAt(new Vector2(30f, 20f)), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(water.SurfaceAt(new Vector2(30f, 20f)), Is.EqualTo(15f));
        }

        [Test]
        public void AboveTheSeaLevel_IsDry()
        {
            var water = new WaterMap(Terrain(), 15f);

            Assert.That(water.DepthAt(new Vector2(80f, 20f)), Is.EqualTo(0f));
            Assert.That(water.SurfaceAt(new Vector2(80f, 20f)), Is.Null);
            Assert.That(water.SurfaceAt(new Vector2(50f, 20f)), Is.Null, "Ground exactly at the sea level is the shore, not water.");
        }

        [Test]
        public void BeyondTheLand_TheSeaGoesOn_ButHasNoDepth()
        {
            var water = new WaterMap(Terrain(), 15f);

            Assert.That(water.SurfaceAt(new Vector2(-100f, 20f)), Is.EqualTo(15f));
            Assert.That(water.DepthAt(new Vector2(-100f, 20f)), Is.Null);
        }

        [Test]
        public void WithoutASea_EverythingIsDry()
        {
            var water = new WaterMap(Terrain(), null);

            Assert.That(water.DepthAt(new Vector2(0f, 20f)), Is.EqualTo(0f));
            Assert.That(water.SurfaceAt(new Vector2(0f, 20f)), Is.Null);
            Assert.That(water.SurfaceAt(new Vector2(-100f, 20f)), Is.Null);
        }

        [Test]
        public void Of_ReadsTheSeaLevelFromTheSpaceRecord()
        {
            var world = BuildingFixture.Create(new SpaceRecord(BuildingFixture.Space, "Main", SpaceKind.Worldspace, 10f, 12f));
            var terrain = new TerrainGrid(world.Session.World.Records, BuildingFixture.Space);

            Assert.That(WaterMap.Of(world.Session.World.Records, terrain).SeaLevel, Is.EqualTo(12f));
            Assert.That(WaterMap.Of(BuildingFixture.Create().Session.World.Records, terrain).SeaLevel, Is.Null, "No space record, no sea.");
        }
    }
}
