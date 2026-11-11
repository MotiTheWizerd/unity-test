using System;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Cameras;
using Forge.Game.Motion;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class FirstPersonCameraTests
    {
        sealed class StandingStill : IMotor
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

        sealed class Eyes : IEyePoint
        {
            public Vector3? At;

            public bool TryGetEyes(out Vector3 eyes)
            {
                eyes = At ?? default;
                return At.HasValue;
            }
        }

        [Test]
        public void RidesTheLooksEyes_InTheBodysFrame()
        {
            // Eyes 1.5 m up and 0.2 m ahead of the feet; the body faces east, so ahead is +X.
            var camera = new FirstPersonCamera { Head = new Eyes { At = new Vector3(0f, 1.5f, 0.2f) } };

            var pose = camera.Frame(PlayerAt(new Vector3(3f, 50f, -2f), 90f, 0f), 0f);

            AssertNear(pose.Position, new Vector3(3.2f, 51.5f, -2f));
        }

        [Test]
        public void WithoutTheLooksEyes_SitsAtTheEyeHeight()
        {
            var camera = new FirstPersonCamera { Head = new Eyes() };

            AssertNear(camera.Frame(PlayerAt(Vector3.Zero, 0f, 0f), 0f).Position, new Vector3(0f, 1.65f, 0f));
        }

        [Test]
        public void FollowsTheHead_Eased_UntilBegunAfresh()
        {
            var eyes = new Eyes { At = new Vector3(0f, 1.6f, 0f) };
            var camera = new FirstPersonCamera(new FirstPersonSettings(headEase: 0.1f)) { Head = eyes };
            var player = PlayerAt(Vector3.Zero, 0f, 0f);
            camera.Frame(player, 0f);

            eyes.At = new Vector3(0f, 1.4f, 0f);
            var eased = camera.Frame(player, 0.1f).Position.Y;

            Assert.That(eased, Is.EqualTo(1.6f - 0.2f * (1f - (float)Math.Exp(-1.0))).Within(1e-4f), "In its ease it covers two thirds of the way.");
            camera.Begin();
            Assert.That(camera.Frame(player, 0.1f).Position.Y, Is.EqualTo(1.4f).Within(1e-5f), "Begun afresh, it goes straight to the eyes.");
        }

        [Test]
        public void TurningTheBody_NeverLagsTheCamera()
        {
            var camera = new FirstPersonCamera(new FirstPersonSettings(headEase: 1f)) { Head = new Eyes { At = new Vector3(0f, 1.5f, 0.2f) } };
            var player = PlayerAt(Vector3.Zero, 0f, 0f);
            camera.Frame(player, 0f);

            player.PlaceAt(Vector3.Zero, 90f);
            var pose = camera.Frame(player, 0.016f);

            AssertNear(pose.Position, new Vector3(0.2f, 1.5f, 0f));
        }

        [Test]
        public void Settings_RefuseWhatMeansNothing()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FirstPersonSettings(headEase: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FirstPersonSettings(nearClip: 0f));
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
