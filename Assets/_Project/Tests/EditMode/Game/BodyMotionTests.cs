using System;
using System.Numerics;
using Forge.Game.Motion;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class BodyMotionTests
    {
        const float Frame = 1f / 60f;

        // Flat ground at y = 0; with a wall, it moves nowhere over the ground.
        sealed class Ground : IPlayerMotor
        {
            readonly bool _walled;

            public Ground(bool walled = false) => _walled = walled;

            public Vector3 Position { get; private set; }

            public MotorContact Move(Vector3 displacement)
            {
                var next = Position + (_walled ? new Vector3(0f, displacement.Y, 0f) : displacement);
                var grounded = next.Y <= 0f;
                if (grounded)
                    next.Y = 0f;
                Position = next;
                return new MotorContact(grounded, false);
            }

            public void Teleport(Vector3 feet) => Position = feet;
        }

        static PlayerCharacter Standing(IPlayerMotor motor)
        {
            var player = new PlayerCharacter(motor, LocomotionSettings.Default, 1.65f);
            player.PlaceAt(Vector3.Zero, 0f);
            player.Tick(PlayerCommands.None, Frame);
            return player;
        }

        static void Run(PlayerCharacter player, PlayerCommands commands, float seconds)
        {
            for (var t = 0f; t < seconds; t += Frame)
                player.Tick(commands, Frame);
        }

        static readonly PlayerCommands Forward = new PlayerCommands(new Vector2(0f, 1f), Vector2.Zero, false, false);

        static readonly PlayerCommands Jump = new PlayerCommands(Vector2.Zero, Vector2.Zero, sprint: false, jump: true);

        [Test]
        public void Between_SplitsGroundAndHeight()
        {
            var motion = BodyMotion.Between(Vector3.Zero, new Vector3(3f, -1f, 4f), 0.5f, grounded: false);

            Assert.That(motion.GroundSpeed, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(motion.VerticalSpeed, Is.EqualTo(-2f).Within(1e-4f));
            Assert.That(motion.Falling, Is.True);
        }

        [Test]
        public void Between_NeedsTime() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => BodyMotion.Between(Vector3.Zero, Vector3.One, 0f, true));

        [Test]
        public void Standing_IsStill()
        {
            var player = Standing(new Ground());

            Assert.That(player.Motion.GroundSpeed, Is.Zero);
            Assert.That(player.Motion.Grounded, Is.True);
        }

        [Test]
        public void Running_ShowsTheRunSpeed()
        {
            var player = Standing(new Ground());

            Run(player, Forward, 1f);

            Assert.That(player.Motion.GroundSpeed, Is.EqualTo(LocomotionSettings.Default.RunSpeed).Within(1e-3f));
            Assert.That(player.Motion.Grounded, Is.True);
        }

        [Test]
        public void RunningIntoAWall_ShowsStanding()
        {
            var player = Standing(new Ground(walled: true));

            Run(player, Forward, 1f);

            Assert.That(player.Motion.GroundSpeed, Is.Zero, "Where the body went, not where it was asked to go.");
        }

        [Test]
        public void Jumping_RisesThenFalls_ThenLands()
        {
            var player = Standing(new Ground());

            player.Tick(Jump, Frame);
            Assert.That(player.Motion.Rising, Is.True);

            Run(player, PlayerCommands.None, 0.4f);
            Assert.That(player.Motion.Falling, Is.True);

            Run(player, PlayerCommands.None, 1f);
            Assert.That(player.Motion.Grounded, Is.True);
        }

        [Test]
        public void PlacingTheBody_LeavesItStill()
        {
            var player = Standing(new Ground());
            Run(player, Forward, 1f);

            player.PlaceAt(new Vector3(50f, 0f, 50f), 90f);

            Assert.That(player.Motion.GroundSpeed, Is.Zero);
        }
    }

    public class StrideMatchTests
    {
        [Test]
        public void UpToTheFastestClip_BlendsAtTheSpeed_AtTheClipsOwnPace()
        {
            var match = StrideMatch.For(groundSpeed: 3f, bodyScale: 1f, fastestClip: 4f);

            Assert.That(match.BlendSpeed, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(match.Pace, Is.EqualTo(1f));
        }

        [Test]
        public void PastTheFastestClip_ItPlaysFaster()
        {
            var match = StrideMatch.For(groundSpeed: 6f, bodyScale: 1f, fastestClip: 4f);

            Assert.That(match.BlendSpeed, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(match.Pace, Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void ABiggerBody_TakesLongerStrides()
        {
            // A body twice the clips' size covers twice the ground per stride.
            var match = StrideMatch.For(groundSpeed: 4f, bodyScale: 2f, fastestClip: 4f);

            Assert.That(match.BlendSpeed, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(match.Pace, Is.EqualTo(1f));
        }

        [Test]
        public void WithNoMovingClip_ItNeverSpeedsUp()
        {
            var match = StrideMatch.For(groundSpeed: 5f, bodyScale: 1f, fastestClip: 0f);

            Assert.That(match.Pace, Is.EqualTo(1f));
        }

        [Test]
        public void NeedsABodySize() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => StrideMatch.For(1f, 0f, 4f));
    }
}
