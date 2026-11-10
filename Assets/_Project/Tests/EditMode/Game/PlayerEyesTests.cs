using System;
using System.Numerics;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class PlayerEyesTests
    {
        const float ManEyes = 1.72f;

        // A body standing on flat ground at height 0.
        sealed class Standing : IPlayerMotor
        {
            public Vector3 Position { get; private set; }

            public MotorContact Move(Vector3 displacement) => new MotorContact(grounded: true, hitCeiling: false);

            public void Teleport(Vector3 feet) => Position = feet;
        }

        static PlayerCharacter Player() => new PlayerCharacter(new Standing(), LocomotionSettings.Default, ManEyes);

        [Test]
        public void ALooksEyeHeight_IsWhereTheEyesAre()
        {
            var player = Player();

            player.EyeHeight = 1.51f;

            Assert.That(player.Eyes.Y, Is.EqualTo(1.51f).Within(1e-5f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void AnEyeHeight_IsAPositiveNumber(float height)
        {
            var player = Player();

            Assert.Throws<ArgumentOutOfRangeException>(() => player.EyeHeight = height);
            Assert.That(player.EyeHeight, Is.EqualTo(ManEyes));
        }
    }
}
