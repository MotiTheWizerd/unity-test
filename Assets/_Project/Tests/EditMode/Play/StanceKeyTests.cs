using Forge.Play;
using NUnit.Framework;

namespace Forge.Tests.Play
{
    public class StanceKeyTests
    {
        [Test]
        public void AStancesKey_IsItsNamesOwn_NeverTheSetsOwn_AndHeldExactlyByATransition()
        {
            var bow = HumanoidAnimation.StanceKey("BowIdle");

            Assert.That(HumanoidAnimation.StanceKey("BowIdle"), Is.EqualTo(bow), "The builder and the player agree by the name alone.");
            Assert.That(bow, Is.Not.Zero, "0 is the set's own drawn moves.");
            Assert.That(bow, Is.InRange(1, 1 << 16).And.Not.EqualTo(HumanoidAnimation.StanceKey("StaffIdle")));
            Assert.That((float)bow, Is.EqualTo(bow), "A float threshold holds it exactly.");
            Assert.That(HumanoidAnimation.StanceGround("BowIdle"), Is.EqualTo("BowIdle Ground"));
        }
    }
}
