using System;
using Forge.Editing.Geometry;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class FlySpeedTests
    {
        [Test]
        public void StartsAtNormalSpeed_WithoutFollowingHeight()
        {
            var speed = new FlySpeed();

            Assert.That(speed.Multiplier, Is.EqualTo(1f));
            Assert.That(speed.FollowsHeight, Is.False);
            Assert.That(speed.FactorAt(500f), Is.EqualTo(1f));
        }

        [Test]
        public void EveryPreset_IsAStep()
        {
            foreach (var preset in FlySpeed.Presets)
                Assert.That(FlySpeed.Steps, Has.Member(preset));
        }

        [Test]
        public void Set_PicksThePreset_OrTheNearestStep()
        {
            var speed = new FlySpeed();

            speed.Set(3f);
            Assert.That(speed.Multiplier, Is.EqualTo(3f));

            speed.Set(7f);
            Assert.That(speed.Multiplier, Is.EqualTo(8f));

            speed.Set(0.01f);
            Assert.That(speed.Multiplier, Is.EqualTo(FlySpeed.Steps[0]));

            Assert.Throws<ArgumentOutOfRangeException>(() => speed.Set(0f));
        }

        [Test]
        public void Step_MovesThroughTheSteps_AndStopsAtTheEnds()
        {
            var speed = new FlySpeed();

            speed.Step(1);
            Assert.That(speed.Multiplier, Is.EqualTo(2f));
            speed.Step(2);
            Assert.That(speed.Multiplier, Is.EqualTo(5f));
            speed.Step(-4);
            Assert.That(speed.Multiplier, Is.EqualTo(0.5f));

            speed.Step(-10);
            Assert.That(speed.Multiplier, Is.EqualTo(FlySpeed.Steps[0]));
            speed.Step(100);
            Assert.That(speed.Multiplier, Is.EqualTo(FlySpeed.Steps[FlySpeed.Steps.Count - 1]));
        }

        [Test]
        public void Changed_IsRaisedOnlyWhenSomethingChanges()
        {
            var speed = new FlySpeed();
            var raised = 0;
            speed.Changed += () => raised++;

            speed.Set(1f);
            speed.Step(-100);
            speed.Step(-1);
            speed.FollowsHeight = true;
            speed.FollowsHeight = true;

            Assert.That(raised, Is.EqualTo(2));
        }

        [Test]
        public void HeightFactor_IsOneNearTheGround_ThenGrowsWithHeight_UpToTheCap()
        {
            Assert.That(FlySpeed.HeightFactor(0f), Is.EqualTo(1f));
            Assert.That(FlySpeed.HeightFactor(FlySpeed.ReferenceHeight), Is.EqualTo(1f));
            Assert.That(FlySpeed.HeightFactor(4f * FlySpeed.ReferenceHeight), Is.EqualTo(4f).Within(1e-5f));
            Assert.That(FlySpeed.HeightFactor(10000f), Is.EqualTo(FlySpeed.MaxHeightFactor));
            Assert.That(FlySpeed.HeightFactor(float.NaN), Is.EqualTo(1f));
        }

        [Test]
        public void FactorAt_MultipliesTheHeight_OnlyWhenFollowingIt_AndKnown()
        {
            var speed = new FlySpeed();
            speed.Set(2f);
            speed.FollowsHeight = true;

            Assert.That(speed.FactorAt(4f * FlySpeed.ReferenceHeight), Is.EqualTo(8f).Within(1e-5f));
            Assert.That(speed.FactorAt(null), Is.EqualTo(2f));
        }

        [Test]
        public void Label_ReadsLikeAMultiplier()
        {
            Assert.That(FlySpeed.Label(2f), Is.EqualTo("×2"));
            Assert.That(FlySpeed.Label(0.5f), Is.EqualTo("×0.5"));
            Assert.That(FlySpeed.Label(0.25f), Is.EqualTo("×0.25"));
        }
    }
}
