using Forge.Play;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Play
{
    public class BackRestTests
    {
        [Test]
        public void OnAHunchedBack_ItLeansIn_RestingOnTheTwoPointsUnderItsMiddle()
        {
            // Depth behind the body along the line, from the seat up to the shoulders: the upper back bulges out.
            var profile = new[]
            {
                new Vector2(-0.4f, 0.14f), new Vector2(-0.2f, 0.17f), new Vector2(0f, 0.22f), new Vector2(0.2f, 0.25f), new Vector2(0.3f, 0.24f),
            };

            Assert.That(BackFinder.TryRest(profile, out var depth, out var slope), Is.True);

            // Resting on the point under its middle it could rock from the bulge above (0.15) to the seat below (0.2).
            Assert.That(depth, Is.EqualTo(0.22f).Within(1e-4f), "It touches the back under its middle...");
            Assert.That(slope, Is.EqualTo(0.175f).Within(1e-4f), "...leaning in below, midway between the ways it could rock.");
            foreach (var point in profile)
                Assert.That(depth + slope * point.x, Is.GreaterThanOrEqualTo(point.y - 1e-4f), $"Nothing of the back pokes through at {point.x}.");
        }

        [Test]
        public void BetweenTwoBulges_ItRestsOnBoth()
        {
            // The shoulder blades above and the seat below stand out; the small of the back between them doesn't.
            var profile = new[] { new Vector2(-0.4f, 0.2f), new Vector2(0f, 0.1f), new Vector2(0.4f, 0.3f) };

            Assert.That(BackFinder.TryRest(profile, out var depth, out var slope), Is.True);

            Assert.That(depth, Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(slope, Is.EqualTo(0.125f).Within(1e-4f));
        }

        [Test]
        public void OnOnePoint_ItStandsLevel()
        {
            Assert.That(BackFinder.TryRest(new[] { new Vector2(0.1f, 0.2f) }, out var depth, out var slope), Is.True);

            Assert.That((depth, slope), Is.EqualTo((0.2f, 0f)));
        }

        [Test]
        public void WithNoBack_ItCantRest()
        {
            Assert.That(BackFinder.TryRest(new Vector2[0], out _, out _), Is.False);
        }
    }
}
