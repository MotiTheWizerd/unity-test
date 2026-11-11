using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Combat;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class RevivalTests
    {
        static ActorAttributes Make()
        {
            var set = AttributeSet.From(new[]
            {
                new AttributeRecord(CoreAttributes.Health, "Health", AttributeKind.Resource, 100f),
                new AttributeRecord(CoreAttributes.Stamina, "Stamina", AttributeKind.Resource, 50f),
                new AttributeRecord(CoreAttributes.CarryWeight, "Carry Weight", AttributeKind.Stat, 250f),
            });
            return new ActorAttributes(set, ActorId.Player);
        }

        [Test]
        public void WakingUp_FillsEveryResource_AndLeavesStatsAlone()
        {
            var attributes = Make();
            attributes.Drain(CoreAttributes.Health, 100f);
            attributes.Drain(CoreAttributes.Stamina, 30f);

            Revival.WakeUp(attributes);

            Assert.That(attributes.Current(CoreAttributes.Health), Is.EqualTo(100f));
            Assert.That(attributes.Current(CoreAttributes.Stamina), Is.EqualTo(50f));
            Assert.That(attributes.Value(CoreAttributes.CarryWeight), Is.EqualTo(250f));
        }

        [Test]
        public void AnActorWithoutAttributes_HasNothingToWake() => Assert.DoesNotThrow(() => Revival.WakeUp(null));
    }
}
