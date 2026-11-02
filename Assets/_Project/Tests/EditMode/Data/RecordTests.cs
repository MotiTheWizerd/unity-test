using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
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
            var record = new PlacedObjectRecord(PlacedId, TreeId, new Vector3(1, 2, 3), rotation, 1.5f);

            Assert.That(record.BaseId, Is.EqualTo(TreeId));
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
                new PlacedObjectRecord(PlacedId, TreeId, Vector3.Zero, Quaternion.Identity, scale));
        }

        [Test]
        public void PlacedObject_RequiresBaseId()
        {
            Assert.Throws<ArgumentException>(() =>
                new PlacedObjectRecord(PlacedId, default, Vector3.Zero, Quaternion.Identity, 1f));
        }

        [Test]
        public void DefaultRegistry_MapsBuiltInTypesBothWays()
        {
            var registry = RecordTypeRegistry.CreateDefault();

            Assert.That(registry.GetKey(typeof(PlacedObjectRecord)), Is.EqualTo("placed_object"));
            Assert.That(registry.TryGetType("base_object", out var type), Is.True);
            Assert.That(type, Is.EqualTo(typeof(BaseObjectRecord)));
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
