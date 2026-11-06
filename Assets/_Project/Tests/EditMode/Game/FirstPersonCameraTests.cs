using System;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Cameras;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class FirstPersonCameraTests
    {
        sealed class StandingStill : IPlayerMotor
        {
            public Vector3 Position { get; private set; }

            public MotorContact Move(Vector3 displacement) => new MotorContact(true, false);

            public void Teleport(Vector3 feet) => Position = feet;
        }

        static PlayerCharacter PlayerAt(Vector3 feet, float heading, float pitch)
        {
            var player = new PlayerCharacter(new StandingStill(), LocomotionSettings.Default, 1.65f);
            player.PlaceAt(feet, heading);
            player.Look.Set(heading, pitch);
            return player;
        }

        static void AssertNear(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{actual} is not {expected}");

        [Test]
        public void SitsAtTheEyes()
        {
            var pose = new FirstPersonCamera().Frame(PlayerAt(new Vector3(3f, 50f, -2f), 0f, 0f), 0.016f);

            AssertNear(pose.Position, new Vector3(3f, 51.65f, -2f));
        }

        [Test]
        public void LooksAlongTheHeading()
        {
            var camera = new FirstPersonCamera();

            AssertNear(camera.Frame(PlayerAt(Vector3.Zero, 0f, 0f), 0f).Forward, Vector3.UnitZ);
            AssertNear(camera.Frame(PlayerAt(Vector3.Zero, 90f, 0f), 0f).Forward, Vector3.UnitX);
            AssertNear(camera.Frame(PlayerAt(Vector3.Zero, 180f, 0f), 0f).Forward, -Vector3.UnitZ);
        }

        [Test]
        public void PitchUp_LooksUp_AndKeepsTheHeading()
        {
            var forward = new FirstPersonCamera().Frame(PlayerAt(Vector3.Zero, 90f, 30f), 0f).Forward;

            Assert.That(forward.Y, Is.EqualTo((float)Math.Sin(30.0 * Math.PI / 180.0)).Within(1e-4f));
            Assert.That(Compass.HeadingOf(new Vector2(forward.X, forward.Z)), Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void HeadingOf_IsTheCompassBearing()
        {
            Assert.That(Compass.HeadingOf(new Vector2(0f, 1f)), Is.EqualTo(0f));
            Assert.That(Compass.HeadingOf(new Vector2(1f, 0f)), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(Compass.HeadingOf(new Vector2(-1f, 0f)), Is.EqualTo(270f).Within(1e-4f));
            Assert.That(Compass.HeadingOf(Vector2.Zero), Is.Zero);
        }
    }
}
