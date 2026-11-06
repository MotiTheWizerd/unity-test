using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class PlayerLookTests
    {
        [Test]
        public void Turn_WrapsTheHeading_AroundTheCompass()
        {
            var look = new PlayerLook(350f);

            look.Turn(new Vector2(20f, 0f));
            Assert.That(look.Heading, Is.EqualTo(10f).Within(1e-4f));

            look.Turn(new Vector2(-30f, 0f));
            Assert.That(look.Heading, Is.EqualTo(340f).Within(1e-4f));
        }

        [Test]
        public void Pitch_StopsShortOfStraightUpAndDown()
        {
            var look = new PlayerLook();

            look.Turn(new Vector2(0f, 200f));
            Assert.That(look.Pitch, Is.EqualTo(PlayerLook.MaxPitch));

            look.Turn(new Vector2(0f, -500f));
            Assert.That(look.Pitch, Is.EqualTo(-PlayerLook.MaxPitch));
        }

        [Test]
        public void Nonsense_LooksNorthAndLevel()
        {
            var look = new PlayerLook(float.NaN, float.NaN);

            Assert.That(look.Heading, Is.Zero);
            Assert.That(look.Pitch, Is.Zero);
        }

        [Test]
        public void Compass_NorthIsPlusZ_EastIsPlusX()
        {
            Assert.That(Vector2.Distance(Compass.Forward(0f), new Vector2(0f, 1f)), Is.LessThan(1e-6f));
            Assert.That(Vector2.Distance(Compass.Forward(90f), new Vector2(1f, 0f)), Is.LessThan(1e-6f));
            Assert.That(Vector2.Distance(Compass.Right(0f), new Vector2(1f, 0f)), Is.LessThan(1e-6f));
            Assert.That(Vector2.Distance(Compass.Right(90f), new Vector2(0f, -1f)), Is.LessThan(1e-6f));
            Assert.That(Compass.Normalize(-90f), Is.EqualTo(270f));
        }
    }
}
