using System;
using Forge.Data;
using Forge.Data.Records;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    public class AttributeRecordTests
    {
        static readonly RecordId Health = AttributeRecord.IdFor("core", "health");
        static readonly RecordId Regen = AttributeRecord.IdFor("core", "health_regen");
        static readonly RecordId Delay = AttributeRecord.IdFor("core", "health_regen_delay");

        [Test]
        public void AResource_ReadsItsRegen_AndAStatItsTerms()
        {
            var health = new AttributeRecord(Health, "Health", AttributeKind.Resource, 100f, regenRate: Regen, regenDelay: Delay);
            var carry = new AttributeRecord(AttributeRecord.IdFor("core", "carry"), "Carry", AttributeKind.Stat, 250f,
                terms: new[] { new AttributeTerm(Health, 0.5f) });

            Assert.That(health.Reads(), Is.EqualTo(new[] { Regen, Delay }));
            Assert.That(carry.Reads(), Is.EqualTo(new[] { Health }));
        }

        [Test]
        public void NoTerms_AreKeptAsNone()
        {
            var stat = new AttributeRecord(Regen, "Regen", AttributeKind.Stat, 1f, terms: Array.Empty<AttributeTerm>());

            Assert.That(stat.Terms, Is.Null);
            Assert.That(stat.Reads(), Is.Empty);
        }

        [Test]
        public void RejectsWhatCannotBe()
        {
            Assert.Throws<ArgumentException>(() => new AttributeRecord(RecordId.Parse("core:health"), "Health", AttributeKind.Stat, 1f));
            Assert.Throws<ArgumentException>(() => new AttributeRecord(Health, " ", AttributeKind.Stat, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttributeRecord(Health, "Health", (AttributeKind)7, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttributeRecord(Health, "Health", AttributeKind.Stat, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttributeRecord(Health, "Health", AttributeKind.Stat, 1f, min: 5f, max: 2f));
            Assert.Throws<ArgumentException>(() => new AttributeRecord(Health, "Health", AttributeKind.Stat, 1f, terms: new[] { new AttributeTerm(Health, 1f) }),
                "Adds to itself.");
            Assert.Throws<ArgumentException>(() => new AttributeRecord(Health, "Health", AttributeKind.Stat, 1f, regenRate: Regen),
                "Only a resource refills.");
            Assert.Throws<ArgumentException>(() => new AttributeRecord(Health, "Health", AttributeKind.Resource, 1f, regenRate: Health),
                "Refills by itself.");
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttributeTerm(Health, float.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => new AttributeTerm(default, 1f));
        }
    }
}
