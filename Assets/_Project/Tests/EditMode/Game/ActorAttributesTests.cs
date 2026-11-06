using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Events;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class ActorAttributesTests
    {
        static readonly RecordId Stamina = Id("stamina");
        static readonly RecordId Regen = Id("stamina_regen");
        static readonly RecordId Delay = Id("stamina_regen_delay");
        static readonly RecordId Carry = Id("carry_weight");
        static readonly RecordId Armor = Id("armor");

        static readonly ActorId Owner = new ActorId("guard");

        static readonly ModifierSource Helmet = new ModifierSource("item:helmet");
        static readonly ModifierSource Potion = new ModifierSource("effect:potion");

        static RecordId Id(string name) => AttributeRecord.IdFor("core", name);

        // Stamina 100 refilling 10 a second after 2 s; carry weight 250 + half the stamina; armor 0..100.
        static AttributeRecord[] Records() => new[]
        {
            new AttributeRecord(Stamina, "Stamina", AttributeKind.Resource, 100f, regenRate: Regen, regenDelay: Delay),
            new AttributeRecord(Regen, "Stamina Regen", AttributeKind.Stat, 10f),
            new AttributeRecord(Delay, "Stamina Regen Delay", AttributeKind.Stat, 2f),
            new AttributeRecord(Carry, "Carry Weight", AttributeKind.Stat, 250f, terms: new[] { new AttributeTerm(Stamina, 0.5f) }),
            new AttributeRecord(Armor, "Armor", AttributeKind.Stat, 0f, max: 100f),
        };

        static ActorAttributes Actor() => new ActorAttributes(AttributeSet.From(Records()), Owner);

        [Test]
        public void StartsFromTheBase_PlusItsTerms()
        {
            var actor = Actor();

            Assert.That(actor.Value(Stamina), Is.EqualTo(100f));
            Assert.That(actor.Current(Stamina), Is.EqualTo(100f));
            Assert.That(actor.Value(Carry), Is.EqualTo(300f));
        }

        [Test]
        public void Modifiers_AddThenPercentThenMultiply_WithinTheMax()
        {
            var actor = Actor();

            actor.Add(Helmet, AttributeModifier.Add(Armor, 20f));
            actor.Add(Helmet, AttributeModifier.Percent(Armor, 25f));
            actor.Add(Potion, AttributeModifier.Percent(Armor, 25f));
            actor.Add(Potion, AttributeModifier.Multiply(Armor, 2f));
            Assert.That(actor.Value(Armor), Is.EqualTo(20f * 1.5f * 2f).Within(1e-4f));

            actor.Add(Potion, AttributeModifier.Add(Armor, 100f));
            Assert.That(actor.Value(Armor), Is.EqualTo(100f), "Never past the record's max.");
        }

        [Test]
        public void RemovingASource_TakesExactlyItsModifiers()
        {
            var actor = Actor();
            actor.Add(Helmet, AttributeModifier.Add(Armor, 20f));
            actor.Add(Potion, AttributeModifier.Add(Armor, 5f));

            var removed = actor.Remove(Helmet);

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(actor.Value(Armor), Is.EqualTo(5f));
            Assert.That(actor.Has(Helmet), Is.False);
            Assert.That(actor.Has(Potion), Is.True);
        }

        [Test]
        public void AChange_RipplesToWhatReadsIt_TellingOfEach()
        {
            var actor = Actor();
            var changes = new List<AttributeChange>();
            actor.Changed += changes.Add;

            actor.Add(Helmet, AttributeModifier.Add(Stamina, 20f));

            Assert.That(actor.Value(Carry), Is.EqualTo(310f));
            Assert.That(changes.Select(c => c.Attribute), Is.EquivalentTo(new[] { Stamina, Carry }));
        }

        [Test]
        public void ANewBase_IsTheActorsOwn()
        {
            var actor = Actor();

            actor.SetBase(Stamina, 150f);

            Assert.That(actor.Base(Stamina), Is.EqualTo(150f));
            Assert.That(actor.Value(Carry), Is.EqualTo(325f));
            Assert.That(Actor().Value(Stamina), Is.EqualTo(100f), "Other actors keep the record's.");
        }

        [Test]
        public void Drain_TakesWhatThereIs_TrySpend_AllOrNothing()
        {
            var actor = Actor();

            Assert.That(actor.Drain(Stamina, 70f), Is.EqualTo(70f));
            Assert.That(actor.TrySpend(Stamina, 40f), Is.False);
            Assert.That(actor.Current(Stamina), Is.EqualTo(30f));
            Assert.That(actor.Drain(Stamina, 40f), Is.EqualTo(30f));
            Assert.That(actor.Current(Stamina), Is.Zero);
            Assert.That(actor.Fraction(Stamina), Is.Zero);
        }

        [Test]
        public void RunningOut_IsToldOnce()
        {
            var actor = Actor();
            var depleted = 0;
            actor.Depleted += _ => depleted++;

            actor.Drain(Stamina, 150f);
            actor.Drain(Stamina, 10f);

            Assert.That(depleted, Is.EqualTo(1));
        }

        [Test]
        public void ABiggerPool_LiftsWhatIsLeft()
        {
            var actor = Actor();
            actor.Drain(Stamina, 40f);

            actor.Add(Potion, AttributeModifier.Add(Stamina, 50f));

            Assert.That(actor.Value(Stamina), Is.EqualTo(150f));
            Assert.That(actor.Current(Stamina), Is.EqualTo(110f));
        }

        [Test]
        public void ASmallerPool_NeverLeavesMoreThanItHolds()
        {
            var actor = Actor();

            actor.Add(Potion, AttributeModifier.Multiply(Stamina, 0.5f));

            Assert.That(actor.Current(Stamina), Is.EqualTo(50f));
        }

        [Test]
        public void Refills_OnlyAfterTheDelay_AtTheRate_NeverPastTheMost()
        {
            var actor = Actor();
            actor.Drain(Stamina, 50f);

            actor.Tick(1.5f);
            Assert.That(actor.Current(Stamina), Is.EqualTo(50f), "Still within the 2 s delay.");

            actor.Tick(1f);
            Assert.That(actor.Current(Stamina), Is.EqualTo(55f).Within(1e-4f), "Half the tick was past the delay.");

            actor.Tick(10f);
            Assert.That(actor.Current(Stamina), Is.EqualTo(100f));
        }

        [Test]
        public void SpendingAgain_HoldsTheRefillBack()
        {
            var actor = Actor();
            actor.Drain(Stamina, 50f);
            actor.Tick(1.9f);

            actor.Drain(Stamina, 0f);
            actor.Tick(1.9f);

            Assert.That(actor.Current(Stamina), Is.EqualTo(50f));
        }

        [Test]
        public void TheRegen_IsAnAttribute_ThatModifiersChange()
        {
            var actor = Actor();
            actor.Drain(Stamina, 50f);
            actor.Add(Potion, AttributeModifier.Multiply(Regen, 2f));
            actor.Add(Potion, AttributeModifier.Add(Delay, -2f));

            actor.Tick(1f);

            Assert.That(actor.Current(Stamina), Is.EqualTo(70f).Within(1e-4f));
        }

        [Test]
        public void ATimedModifier_WearsOff_TellingOfIt()
        {
            var actor = Actor();
            actor.Add(Potion, AttributeModifier.Add(Armor, 30f), seconds: 5f);
            var changes = new List<AttributeChange>();
            actor.Changed += changes.Add;

            actor.Tick(4f);
            Assert.That(actor.Value(Armor), Is.EqualTo(30f));

            actor.Tick(2f);
            Assert.That(actor.Value(Armor), Is.Zero);
            Assert.That(actor.Has(Potion), Is.False);
            Assert.That(changes.Single().Attribute, Is.EqualTo(Armor));
        }

        [Test]
        public void OnlyResources_AreSpent()
        {
            var actor = Actor();

            Assert.Throws<InvalidOperationException>(() => actor.Drain(Armor, 1f));
            Assert.Throws<InvalidOperationException>(() => actor.Restore(Carry, 1f));
            Assert.Throws<KeyNotFoundException>(() => actor.Value(Id("nothing")));
        }

        [Test]
        public void TheSet_RefusesUnknownAttributes_AndCircles()
        {
            var unknown = new AttributeRecord(Carry, "Carry", AttributeKind.Stat, 1f, terms: new[] { new AttributeTerm(Id("strength"), 1f) });
            var notAStat = new AttributeRecord(Stamina, "Stamina", AttributeKind.Resource, 1f, regenRate: Armor);
            var armorAsResource = new AttributeRecord(Armor, "Armor", AttributeKind.Resource, 1f);
            var a = new AttributeRecord(Id("a"), "A", AttributeKind.Stat, 1f, terms: new[] { new AttributeTerm(Id("b"), 1f) });
            var b = new AttributeRecord(Id("b"), "B", AttributeKind.Stat, 1f, terms: new[] { new AttributeTerm(Id("c"), 1f) });
            var c = new AttributeRecord(Id("c"), "C", AttributeKind.Stat, 1f, terms: new[] { new AttributeTerm(Id("a"), 1f) });

            Assert.Throws<DataValidationException>(() => AttributeSet.From(new[] { unknown }));
            Assert.Throws<DataValidationException>(() => AttributeSet.From(new[] { notAStat, armorAsResource }));
            Assert.Throws<DataValidationException>(() => AttributeSet.From(new[] { unknown, unknown }));
            var circle = Assert.Throws<DataValidationException>(() => AttributeSet.From(new[] { a, b, c }));
            StringAssert.Contains("circle", circle.Message);
        }

        [Test]
        public void ManyActors_ShareOneSet_EachWithItsOwnNumbers()
        {
            var set = AttributeSet.From(Records());
            var tired = new ActorAttributes(set, new ActorId("tired"));
            var fresh = new ActorAttributes(set, new ActorId("fresh"));

            tired.Drain(Stamina, 60f);

            Assert.That(fresh.Current(Stamina), Is.EqualTo(100f));
            Assert.That(tired.Current(Stamina), Is.EqualTo(40f));
        }

        [Test]
        public void FillingUp_IsToldOnce_WhenItReachesTheMost()
        {
            var actor = Actor();
            var refilled = new List<RecordId>();
            actor.Refilled += refilled.Add;
            actor.Drain(Stamina, 30f);

            actor.Restore(Stamina, 20f);
            Assert.That(refilled, Is.Empty);

            actor.Restore(Stamina, 50f);
            actor.Restore(Stamina, 50f);
            Assert.That(refilled, Is.EqualTo(new[] { Stamina }));
        }

        [Test]
        public void AttributesBelongToAnActor() =>
            Assert.Throws<ArgumentException>(() => new ActorAttributes(AttributeSet.From(Records()), default));

        [Test]
        public void TheStream_HearsWhatHappens_NamedByTheOwner_NotEveryNumber()
        {
            var actor = Actor();
            var events = new GameEvents();
            var heard = new List<string>();
            events.SubscribeAll(e => heard.Add(e.ToString()));
            var binding = AttributeEvents.Bind(actor, events);

            actor.Drain(Stamina, 40f);
            actor.Drain(Stamina, 60f);
            actor.Tick(20f);
            events.Flush();

            Assert.That(heard, Is.EqualTo(new[]
            {
                new ResourceDepleted(Owner, Stamina).ToString(),
                new ResourceRefilled(Owner, Stamina).ToString(),
            }), "Two happenings; the refill's many small steps stay off the stream.");

            binding.Dispose();
            actor.Drain(Stamina, 100f);
            events.Flush();
            Assert.That(heard, Has.Count.EqualTo(2), "Unbound, it tells no more.");
        }
    }
}
