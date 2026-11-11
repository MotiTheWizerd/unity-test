using System;
using Forge.Game.Motion;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class BodyShapeTests
    {
        [Test]
        public void KeepsWhatIsSound()
        {
            var shape = new BodyShape(1.8f, 0.3f, 1f, 0.4f, 50f);

            Assert.That((shape.Height, shape.Radius, shape.CrouchHeight, shape.StepHeight, shape.SlopeLimit),
                Is.EqualTo((1.8f, 0.3f, 1f, 0.4f, 50f)));
        }

        [Test]
        public void NeverWiderThanTall_CrouchedBetweenItsWidthAndHeight_StepWithinItsSides()
        {
            var squat = new BodyShape(1f, 0.8f, 0.1f, 2f, 120f);

            Assert.That(squat.Radius, Is.EqualTo(0.5f));
            Assert.That(squat.CrouchHeight, Is.EqualTo(1f));
            Assert.That(squat.StepHeight, Is.EqualTo(0f));
            Assert.That(squat.SlopeLimit, Is.EqualTo(89f));
            Assert.That(new BodyShape(1.8f, 0.3f, 3f, 0.4f, 50f).CrouchHeight, Is.EqualTo(1.8f));
        }

        [Test]
        public void WithHeight_KeepsItsWidthStepAndSlope_CrouchingToTheSameShare()
        {
            var shorter = new BodyShape(1.8f, 0.3f, 0.9f, 0.4f, 50f).WithHeight(1.6f);

            Assert.That(shorter.Height, Is.EqualTo(1.6f));
            Assert.That(shorter.CrouchHeight, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That((shorter.Radius, shorter.StepHeight, shorter.SlopeLimit), Is.EqualTo((0.3f, 0.4f, 50f)));
        }

        [Test]
        public void ItsGuard_IsNoNarrowerThanItself_FitsItsCrouch_AndKeepsWithItsHeight()
        {
            Assert.That(new BodyShape(1.8f, 0.3f, 1f, 0.4f, 50f).GuardRadius, Is.EqualTo(0.3f), "None given: as wide as ever.");
            Assert.That(new BodyShape(1.8f, 0.3f, 1f, 0.4f, 50f, guardRadius: 0.45f).GuardRadius, Is.EqualTo(0.45f));
            Assert.That(new BodyShape(1.8f, 0.3f, 1f, 0.4f, 50f, guardRadius: 0.2f).GuardRadius, Is.EqualTo(0.3f));
            Assert.That(new BodyShape(1.8f, 0.3f, 0.8f, 0.4f, 50f, guardRadius: 0.6f).GuardRadius, Is.EqualTo(0.4f), "Crouched, still a capsule.");
            Assert.That(new BodyShape(1.8f, 0.3f, 1f, 0.4f, 50f, guardRadius: 0.45f).ForEyes(1.5f).GuardRadius, Is.EqualTo(0.45f));
        }

        [Test]
        public void ForEyes_IsAHeadTallerThanItsEyes()
        {
            var shape = new BodyShape(1.8f, 0.3f, 1f, 0.4f, 50f).ForEyes(1.51f);

            Assert.That(shape.Height, Is.EqualTo(1.51f + BodyShape.HeadAboveEyes).Within(1e-5f));
        }

        [Test]
        public void RefusesNoBody()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BodyShape(0f, 0.3f, 1f, 0.4f, 50f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BodyShape(1.8f, 0f, 1f, 0.4f, 50f));
        }
    }
}
