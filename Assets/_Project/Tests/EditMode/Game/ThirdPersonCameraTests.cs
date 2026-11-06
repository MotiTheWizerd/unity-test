using System;
using System.Numerics;
using Forge.Game.Cameras;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class ThirdPersonCameraTests
    {
        const float Frame = 1f / 60f;

        // Centred behind the player, so the expected spots are easy to read.
        static readonly ThirdPersonSettings Centred = new ThirdPersonSettings(distance: 4f, minDistance: 1f, maxDistance: 8f, zoomStep: 1f, pivotHeight: 1.5f, shoulderOffset: 0f, returnSpeed: 10f);

        sealed class StandingStill : IPlayerMotor
        {
            public Vector3 Position { get; private set; }

            public MotorContact Move(Vector3 displacement) => new MotorContact(true, false);

            public void Teleport(Vector3 feet) => Position = feet;
        }

        // A wall across the whole world at z = WallZ (the camera stops Radius short of it), or nothing.
        sealed class WallBehind : ICameraObstacles
        {
            public float? WallZ { get; set; }

            public float Clearance(Vector3 from, Vector3 to, float radius)
            {
                var length = Vector3.Distance(from, to);
                if (!WallZ.HasValue || to.Z >= WallZ.Value + radius || from.Z <= WallZ.Value)
                    return length;
                return Math.Max(0f, (from.Z - (WallZ.Value + radius)) / (from.Z - to.Z) * length);
            }
        }

        static PlayerCharacter PlayerAt(Vector3 feet, float heading = 0f, float pitch = 0f)
        {
            var player = new PlayerCharacter(new StandingStill(), LocomotionSettings.Default, 1.65f);
            player.PlaceAt(feet, heading);
            player.Look.Set(heading, pitch);
            return player;
        }

        static void AssertNear(Vector3 actual, Vector3 expected, float tolerance = 1e-3f) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(tolerance), $"{actual} is not {expected}");

        [Test]
        public void SitsBehindThePivot_LookingAlongTheLook()
        {
            var pose = new ThirdPersonCamera(Centred).Frame(PlayerAt(new Vector3(0f, 10f, 0f)), Frame);

            AssertNear(pose.Position, new Vector3(0f, 11.5f, -4f));
            AssertNear(pose.Forward, Vector3.UnitZ);
        }

        [Test]
        public void LookingDown_RisesBehind()
        {
            var pose = new ThirdPersonCamera(Centred).Frame(PlayerAt(Vector3.Zero, 0f, -30f), Frame);

            Assert.That(pose.Position.Y, Is.EqualTo(1.5f + 4f * 0.5f).Within(1e-3f));
            Assert.That(pose.Forward.Y, Is.LessThan(0f));
        }

        [Test]
        public void TheShoulder_SitsToTheRightOfTheLook()
        {
            var settings = new ThirdPersonSettings(distance: 3f, shoulderOffset: 0.5f, pivotHeight: 1.5f);

            var pose = new ThirdPersonCamera(settings).Frame(PlayerAt(Vector3.Zero, 90f), Frame);

            // Facing east, right is south.
            AssertNear(pose.Position, new Vector3(-3f, 1.5f, -0.5f));
        }

        [Test]
        public void AWallBehind_PullsItIn_AtOnce_ThenItEasesBackOut()
        {
            var wall = new WallBehind { WallZ = -2f };
            var camera = new ThirdPersonCamera(Centred, wall);
            var player = PlayerAt(Vector3.Zero);

            var blocked = camera.Frame(player, Frame);
            Assert.That(blocked.Position.Z, Is.EqualTo(-2f + Centred.Radius).Within(1e-3f), "Stops the radius short of the wall.");

            wall.WallZ = null;
            var easing = camera.Frame(player, 0.1f);
            Assert.That(camera.ShownDistance, Is.EqualTo(2f - Centred.Radius + Centred.ReturnSpeed * 0.1f).Within(1e-3f));
            Assert.That(easing.Position.Z, Is.GreaterThan(-4f), "Not back out in one frame.");

            for (var i = 0; i < 60; i++)
                camera.Frame(player, Frame);
            Assert.That(camera.ShownDistance, Is.EqualTo(4f));
        }

        [Test]
        public void Zoom_StaysWithinItsRange()
        {
            var camera = new ThirdPersonCamera(Centred);

            camera.Zoom(2f);
            Assert.That(camera.Distance, Is.EqualTo(2f));
            camera.Zoom(10f);
            Assert.That(camera.Distance, Is.EqualTo(Centred.MinDistance));
            Assert.That(camera.IsClosest, Is.True);
            camera.Zoom(-100f);
            Assert.That(camera.Distance, Is.EqualTo(Centred.MaxDistance));
            camera.Zoom(float.NaN);
            Assert.That(camera.Distance, Is.EqualTo(Centred.MaxDistance));
        }

        [Test]
        public void Settings_RefuseNonsense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ThirdPersonSettings(minDistance: 5f, maxDistance: 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ThirdPersonSettings(radius: 0f));
            Assert.That(new ThirdPersonSettings(distance: 20f, maxDistance: 8f).Distance, Is.EqualTo(8f), "A start distance past the range is held to it.");
        }

        [Test]
        public void Modes_SayHowTheBodyTurns_AndWhetherItShows()
        {
            Assert.That(new FirstPersonCamera().Facing, Is.EqualTo(BodyFacing.Look));
            Assert.That(new FirstPersonCamera().ShowsBody, Is.False);
            Assert.That(new ThirdPersonCamera().Facing, Is.EqualTo(BodyFacing.Movement));
            Assert.That(new ThirdPersonCamera().ShowsBody, Is.True);
        }
    }
}
