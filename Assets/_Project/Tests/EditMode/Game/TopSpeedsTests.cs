using System;
using System.Numerics;
using Forge.Game.Motion;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class TopSpeedsTests
    {
        [Test]
        public void Ahead_IsForwardAndBack_Aside_EitherSide()
        {
            var top = new TopSpeeds(3f, 1.2f);

            Assert.That(top.Along(new Vector2(0f, 1f)), Is.EqualTo(3f).Within(1e-5f));
            Assert.That(top.Along(new Vector2(0f, -2f)), Is.EqualTo(3f).Within(1e-5f), "Backing away, its length aside.");
            Assert.That(top.Along(new Vector2(5f, 0f)), Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(top.Along(new Vector2(-1f, 0f)), Is.EqualTo(1.2f).Within(1e-5f));
        }

        [Test]
        public void Between_TheEllipseThroughBoth()
        {
            var top = new TopSpeeds(3f, 1.2f);
            var diagonal = top.Along(new Vector2(1f, 1f));

            var way = Vector2.Normalize(new Vector2(1f, 1f)) * diagonal;
            Assert.That(way.X * way.X / (1.2f * 1.2f) + way.Y * way.Y / (3f * 3f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(diagonal, Is.GreaterThan(1.2f).And.LessThan(3f));
        }

        [Test]
        public void NoWay_IsAhead()
        {
            Assert.That(new TopSpeeds(3f, 1.2f).Along(Vector2.Zero), Is.EqualTo(3f));
        }

        [Test]
        public void None_AsTheDefault_HoldsNothingBack()
        {
            Assert.That(float.IsPositiveInfinity(TopSpeeds.None.Along(new Vector2(0.3f, 0.7f))), Is.True);
            Assert.That(float.IsPositiveInfinity(default(TopSpeeds).Ahead), Is.True);
            Assert.That(float.IsPositiveInfinity(default(TopSpeeds).Aside), Is.True);
        }

        [Test]
        public void ALimitOneWayOnly_HoldsOnlyThatWay()
        {
            var aside = new TopSpeeds(float.PositiveInfinity, 1.2f);

            Assert.That(float.IsPositiveInfinity(aside.Along(new Vector2(0f, 1f))), Is.True);
            Assert.That(aside.Along(new Vector2(1f, 1f)), Is.EqualTo(1.2f * (float)Math.Sqrt(2.0)).Within(1e-4f), "Its step aside held to 1.2.");
        }

        [Test]
        public void ATopSpeed_IsMoreThanNothing()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TopSpeeds(0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TopSpeeds(1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TopSpeeds(float.NaN, 1f));
        }
    }
}
