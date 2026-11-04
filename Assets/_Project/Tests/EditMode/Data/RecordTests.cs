using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    public class RecordTests
    {
        static readonly RecordId TreeId = RecordId.Parse("forge.core:tree_oak");
        static readonly RecordId PlacedId = RecordId.Parse("forge.core:ref/tree_oak_001");

        [Test]
        public void BaseObject_StoresValues()
        {
            var record = new BaseObjectRecord(TreeId, "Oak Tree", "trees/oak");

            Assert.That(record.Id, Is.EqualTo(TreeId));
            Assert.That(record.DisplayName, Is.EqualTo("Oak Tree"));
            Assert.That(record.AssetKey, Is.EqualTo("trees/oak"));
        }

        [Test]
        public void BaseObject_CategoryIsNormalizedAndOptional()
        {
            Assert.That(new BaseObjectRecord(TreeId, "Oak", "trees/oak").Category, Is.Empty);
            Assert.That(new BaseObjectRecord(TreeId, "Oak", "trees/oak", "  ").Category, Is.Empty);
            Assert.That(new BaseObjectRecord(TreeId, "Oak", "trees/oak", " Nature / Trees ").Category, Is.EqualTo("Nature/Trees"));
        }

        [TestCase("Nature//Trees")]
        [TestCase("/Trees")]
        [TestCase("Nature/ ")]
        public void BaseObject_RejectsEmptyCategorySegments(string category)
        {
            Assert.Throws<ArgumentException>(() => new BaseObjectRecord(TreeId, "Oak", "trees/oak", category));
        }

        [Test]
        public void BaseObject_PlacementRulesAreOptional()
        {
            var rules = new PlacementRules(true, 0.8f, 1.2f, false);

            Assert.That(new BaseObjectRecord(TreeId, "Oak", "trees/oak").Placement, Is.Null);
            Assert.That(new BaseObjectRecord(TreeId, "Oak", "trees/oak", placement: rules).Placement, Is.SameAs(rules));
        }

        [TestCase(0f, 1f)]
        [TestCase(-1f, 1f)]
        [TestCase(1.2f, 0.8f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(1f, float.PositiveInfinity)]
        public void PlacementRules_RejectInvalidScaleRange(float min, float max)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlacementRules(true, min, max, false));
        }

        [Test]
        public void BaseObject_RequiresFields()
        {
            Assert.Throws<ArgumentException>(() => new BaseObjectRecord(default, "Oak", "trees/oak"));
            Assert.Throws<ArgumentException>(() => new BaseObjectRecord(TreeId, " ", "trees/oak"));
            Assert.Throws<ArgumentException>(() => new BaseObjectRecord(TreeId, "Oak", null));
        }

        [Test]
        public void PlacedObject_StoresValues()
        {
            var rotation = Quaternion.CreateFromYawPitchRoll(1f, 0f, 0f);
            var record = new PlacedObjectRecord(PlacedId, TreeId, TestRecords.Space, new Vector3(1, 2, 3), rotation, 1.5f);

            Assert.That(record.BaseId, Is.EqualTo(TreeId));
            Assert.That(record.Space, Is.EqualTo(TestRecords.Space));
            Assert.That(record.Position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(record.Rotation, Is.EqualTo(rotation));
            Assert.That(record.Scale, Is.EqualTo(1.5f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void PlacedObject_RejectsInvalidScale(float scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlacedObjectRecord(PlacedId, TreeId, TestRecords.Space, Vector3.Zero, Quaternion.Identity, scale));
        }

        [Test]
        public void PlacedObject_RequiresBaseId()
        {
            Assert.Throws<ArgumentException>(() =>
                new PlacedObjectRecord(PlacedId, default, TestRecords.Space, Vector3.Zero, Quaternion.Identity, 1f));
        }

        [Test]
        public void PlacedObject_RequiresSpace()
        {
            Assert.Throws<ArgumentException>(() =>
                new PlacedObjectRecord(PlacedId, TreeId, default, Vector3.Zero, Quaternion.Identity, 1f));
        }

        [Test]
        public void PlacedObject_WithMethods_KeepTheSpace()
        {
            var record = new PlacedObjectRecord(PlacedId, TreeId, TestRecords.OtherSpace, Vector3.Zero, Quaternion.Identity, 1f);

            Assert.That(record.WithPosition(new Vector3(1, 2, 3)).Space, Is.EqualTo(TestRecords.OtherSpace));
            Assert.That(record.WithRotation(Quaternion.CreateFromYawPitchRoll(1f, 0f, 0f)).Space, Is.EqualTo(TestRecords.OtherSpace));
            Assert.That(record.WithScale(2f).Space, Is.EqualTo(TestRecords.OtherSpace));
            Assert.That(record.WithTransform(Vector3.One, Quaternion.Identity, 3f).Space, Is.EqualTo(TestRecords.OtherSpace));
        }

        [Test]
        public void Space_StoresValues()
        {
            var id = SpaceRecord.IdFor("forge.core", "main");
            var space = new SpaceRecord(id, "Tamriel", SpaceKind.Worldspace, 50f);

            Assert.That(id.ToString(), Is.EqualTo("forge.core:space/main"));
            Assert.That(space.DisplayName, Is.EqualTo("Tamriel"));
            Assert.That(space.Kind, Is.EqualTo(SpaceKind.Worldspace));
            Assert.That(space.BaseHeight, Is.EqualTo(50f));
        }

        [Test]
        public void Space_RequiresASpaceNameAndADisplayName()
        {
            Assert.Throws<ArgumentException>(() => new SpaceRecord(RecordId.Parse("core:main"), "Main", SpaceKind.Worldspace));
            Assert.Throws<ArgumentException>(() => new SpaceRecord(RecordId.Parse("core:ref/space/main"), "Main", SpaceKind.Worldspace));
            Assert.Throws<ArgumentException>(() => new SpaceRecord(TestRecords.Space, " ", SpaceKind.Worldspace));
            Assert.Throws<ArgumentException>(() => new SpaceRecord(TestRecords.Space, null, SpaceKind.Worldspace));
        }

        [TestCase(-1f)]
        [TestCase(WorldGrid.MaxTerrainHeight + 1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Space_RejectsBaseHeightOutOfRange(float baseHeight)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpaceRecord(TestRecords.Space, "Main", SpaceKind.Worldspace, baseHeight));
        }

        [Test]
        public void Space_BaseHeightBounds_AreAllowed()
        {
            Assert.That(new SpaceRecord(TestRecords.Space, "Main", SpaceKind.Worldspace, 0f).BaseHeight, Is.EqualTo(0f));
            Assert.That(new SpaceRecord(TestRecords.Space, "Main", SpaceKind.Worldspace, WorldGrid.MaxTerrainHeight).BaseHeight,
                Is.EqualTo(WorldGrid.MaxTerrainHeight));
        }

        [Test]
        public void Space_Interior_HasNoBaseHeight()
        {
            var interior = new SpaceRecord(SpaceRecord.IdFor("core", "house"), "House", SpaceKind.Interior, 80f);

            Assert.That(interior.Kind, Is.EqualTo(SpaceKind.Interior));
            Assert.That(interior.BaseHeight, Is.EqualTo(0f));
        }

        [Test]
        public void Space_WaterLevel_IsOptional_AndChangesAlone()
        {
            var dry = new SpaceRecord(TestRecords.Space, "Main", SpaceKind.Worldspace, 50f);
            Assert.That(dry.WaterLevel, Is.Null);

            var sea = dry.WithWaterLevel(48f);

            Assert.That(sea.WaterLevel, Is.EqualTo(48f));
            Assert.That((sea.Id, sea.DisplayName, sea.Kind, sea.BaseHeight), Is.EqualTo((dry.Id, dry.DisplayName, dry.Kind, dry.BaseHeight)));
            Assert.That(sea.WithWaterLevel(null).WaterLevel, Is.Null);
        }

        [TestCase(-1f)]
        [TestCase(WorldGrid.MaxTerrainHeight + 1f)]
        [TestCase(float.NaN)]
        public void Space_RejectsWaterLevelOutOfRange(float waterLevel)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpaceRecord(TestRecords.Space, "Main", SpaceKind.Worldspace, 50f, waterLevel));
        }

        [Test]
        public void Space_Interior_HasNoSea()
        {
            var interior = new SpaceRecord(SpaceRecord.IdFor("core", "cave"), "Cave", SpaceKind.Interior, 0f, 10f);

            Assert.That(interior.WaterLevel, Is.Null);
        }

        [Test]
        public void DefaultRegistry_MapsBuiltInTypesBothWays()
        {
            var registry = RecordTypeRegistry.CreateDefault();

            Assert.That(registry.GetKey(typeof(PlacedObjectRecord)), Is.EqualTo("placed_object"));
            Assert.That(registry.TryGetType("base_object", out var type), Is.True);
            Assert.That(type, Is.EqualTo(typeof(BaseObjectRecord)));
            Assert.That(registry.GetKey(typeof(SpaceRecord)), Is.EqualTo("space"));
        }

        [Test]
        public void Registry_RejectsUnknownAndDuplicateEntries()
        {
            var registry = RecordTypeRegistry.CreateDefault();

            Assert.That(registry.TryGetType("System.IO.File", out _), Is.False);
            Assert.That(registry.TryGetType(null, out _), Is.False);
            Assert.Throws<KeyNotFoundException>(() => registry.GetKey(typeof(string)));
            Assert.Throws<ArgumentException>(() => registry.Register<BaseObjectRecord>("other_key"));
            Assert.Throws<ArgumentException>(() => registry.Register<PlacedObjectRecord>("base_object"));
            Assert.Throws<ArgumentException>(() => new RecordTypeRegistry().Register<BaseObjectRecord>("Bad Key"));
        }
    }
}
