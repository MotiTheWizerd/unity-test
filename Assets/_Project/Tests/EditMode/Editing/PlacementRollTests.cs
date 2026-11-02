using System;
using Forge.Data.Records;
using Forge.Editing.Placement;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class PlacementRollTests
    {
        static PlacementRules Rules(bool randomYaw, float min, float max) => new PlacementRules(randomYaw, min, max, false);

        [Test]
        public void NoRules_IsIdentity()
        {
            var roll = PlacementRoll.Draw(null, new Random(1));

            Assert.That(roll.YawDegrees, Is.Zero);
            Assert.That(roll.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void CanVary_OnlyWithRandomYawOrAScaleRange()
        {
            Assert.That(PlacementRoll.CanVary(null), Is.False);
            Assert.That(PlacementRoll.CanVary(Rules(true, 1f, 1f)), Is.True);
            Assert.That(PlacementRoll.CanVary(Rules(false, 0.8f, 1.2f)), Is.True);
            Assert.That(PlacementRoll.CanVary(new PlacementRules(false, 1f, 1f, true)), Is.False, "A fence only tilts to the slope.");
        }

        [Test]
        public void Draws_StayInRange_AndVary()
        {
            var rules = Rules(true, 0.8f, 1.25f);
            var random = new Random(42);
            var minYaw = 360f;
            var maxYaw = 0f;
            var minScale = float.MaxValue;
            var maxScale = 0f;

            for (var i = 0; i < 1000; i++)
            {
                var roll = PlacementRoll.Draw(rules, random);
                Assert.That(roll.YawDegrees, Is.InRange(0f, 359f));
                Assert.That(roll.Scale, Is.InRange(0.8f, 1.25f));
                minYaw = Math.Min(minYaw, roll.YawDegrees);
                maxYaw = Math.Max(maxYaw, roll.YawDegrees);
                minScale = Math.Min(minScale, roll.Scale);
                maxScale = Math.Max(maxScale, roll.Scale);
            }

            Assert.That(maxYaw - minYaw, Is.GreaterThan(300f), "Headings cover the circle.");
            Assert.That(maxScale - minScale, Is.GreaterThan(0.4f), "Scales cover the range.");
        }

        [Test]
        public void Draws_AreRoundedForReadableFiles()
        {
            var roll = PlacementRoll.Draw(Rules(true, 0.5f, 2f), new Random(7));

            Assert.That(roll.YawDegrees, Is.EqualTo(Math.Round(roll.YawDegrees)));
            Assert.That(roll.Scale * 100f, Is.EqualTo(Math.Round(roll.Scale * 100f)).Within(1e-3));
        }

        [Test]
        public void WithoutRandomYaw_OnlyScaleVaries()
        {
            var roll = PlacementRoll.Draw(Rules(false, 2f, 2f), new Random(3));

            Assert.That(roll.YawDegrees, Is.Zero);
            Assert.That(roll.Scale, Is.EqualTo(2f));
        }

        [Test]
        public void SameSeed_GivesSameDraws()
        {
            var rules = Rules(true, 0.8f, 1.2f);

            var a = PlacementRoll.Draw(rules, new Random(5));
            var b = PlacementRoll.Draw(rules, new Random(5));

            Assert.That(a.YawDegrees, Is.EqualTo(b.YawDegrees));
            Assert.That(a.Scale, Is.EqualTo(b.Scale));
        }
    }
}
