using System;
using System.Numerics;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class PlayerLocomotionTests
    {
        const float Frame = 1f / 60f;

        static readonly LocomotionSettings Settings = LocomotionSettings.Default;

        static PlayerCommands Walk(float x, float y, bool sprint = false, bool jump = false) =>
            new PlayerCommands(new Vector2(x, y), Vector2.Zero, sprint, jump);

        static PlayerCommands Jump => new PlayerCommands(Vector2.Zero, Vector2.Zero, false, true);

        // A body on endless flat ground at height 0, with an optional ceiling.
        sealed class FlatGround : IPlayerMotor
        {
            readonly float? _ceiling;

            public FlatGround(float? ceiling = null) => _ceiling = ceiling;

            public Vector3 Position { get; private set; }

            public MotorContact Move(Vector3 displacement)
            {
                var next = Position + displacement;
                var hitCeiling = _ceiling.HasValue && next.Y > _ceiling.Value;
                if (hitCeiling)
                    next.Y = _ceiling.Value;
                var grounded = next.Y <= 0f;
                if (grounded)
                    next.Y = 0f;
                Position = next;
                return new MotorContact(grounded, hitCeiling);
            }

            public void Teleport(Vector3 feet) => Position = feet;
        }

        static PlayerCharacter OnFlatGround(FlatGround ground = null)
        {
            var player = new PlayerCharacter(ground ?? new FlatGround(), Settings, 1.65f);
            // One still frame, so it knows it stands.
            player.Tick(PlayerCommands.None, Frame);
            return player;
        }

        static void Run(PlayerCharacter player, PlayerCommands commands, float seconds)
        {
            for (var t = 0f; t < seconds; t += Frame)
                player.Tick(commands, Frame);
        }

        static float GroundSpeed(PlayerCharacter player) =>
            new Vector2(player.Locomotion.Velocity.X, player.Locomotion.Velocity.Z).Length();

        [Test]
        public void Forward_FacingNorth_RunsNorth_AtTheRunPace()
        {
            var player = OnFlatGround();

            Run(player, Walk(0f, 1f), 1f);

            Assert.That(player.Feet.Z, Is.GreaterThan(3.5f));
            Assert.That(Math.Abs(player.Feet.X), Is.LessThan(1e-3f));
            Assert.That(GroundSpeed(player), Is.EqualTo(Settings.RunSpeed).Within(1e-3f));
            Assert.That(player.IsGrounded, Is.True);
        }

        [Test]
        public void Forward_FacingEast_RunsEast()
        {
            var player = OnFlatGround();
            player.Look.Set(90f, 0f);

            Run(player, Walk(0f, 1f), 0.5f);

            Assert.That(player.Feet.X, Is.GreaterThan(1f));
            Assert.That(Math.Abs(player.Feet.Z), Is.LessThan(1e-3f));
        }

        [Test]
        public void Right_FacingNorth_StepsEast()
        {
            var player = OnFlatGround();

            Run(player, Walk(1f, 0f), 0.5f);

            Assert.That(player.Feet.X, Is.GreaterThan(1f));
            Assert.That(Math.Abs(player.Feet.Z), Is.LessThan(1e-3f));
        }

        [Test]
        public void Diagonal_IsNoFasterThanStraight()
        {
            var player = OnFlatGround();

            Run(player, Walk(1f, 1f), 1f);

            Assert.That(GroundSpeed(player), Is.EqualTo(Settings.RunSpeed).Within(1e-3f));
        }

        [Test]
        public void Sprint_RunsAtTheSprintPace()
        {
            var player = OnFlatGround();

            Run(player, Walk(0f, 1f, sprint: true), 1f);

            Assert.That(GroundSpeed(player), Is.EqualTo(Settings.SprintSpeed).Within(1e-3f));
        }

        [Test]
        public void LettingGo_StopsWithinAMoment()
        {
            var player = OnFlatGround();
            Run(player, Walk(0f, 1f), 1f);

            Run(player, PlayerCommands.None, 0.25f);

            Assert.That(GroundSpeed(player), Is.Zero);
        }

        [Test]
        public void Standing_PressesIntoTheGround_WithoutSinking()
        {
            var player = OnFlatGround();

            Run(player, PlayerCommands.None, 1f);

            Assert.That(player.Feet.Y, Is.Zero);
            Assert.That(player.Locomotion.Velocity.Y, Is.EqualTo(-PlayerLocomotion.GroundStickSpeed));
            Assert.That(player.IsGrounded, Is.True);
        }

        [Test]
        public void Jump_RisesToTheJumpHeight_AndLands()
        {
            var player = OnFlatGround();
            var highest = 0f;

            player.Tick(Jump, Frame);
            for (var i = 0; i < 120; i++)
            {
                player.Tick(PlayerCommands.None, Frame);
                highest = Math.Max(highest, player.Feet.Y);
            }

            // Frame by frame the peak lands within half a frame's rise of the exact height.
            Assert.That(highest, Is.EqualTo(Settings.JumpHeight).Within(Settings.JumpSpeed * Frame));
            Assert.That(player.Feet.Y, Is.Zero);
            Assert.That(player.IsGrounded, Is.True);
        }

        [Test]
        public void Jump_IsOncePerLanding()
        {
            var player = OnFlatGround();

            player.Tick(Jump, Frame);
            Run(player, Jump, 0.2f);
            var risingSpeed = player.Locomotion.Velocity.Y;

            Assert.That(risingSpeed, Is.LessThan(Settings.JumpSpeed - 1f), "Pressing again in the air doesn't jump again.");
        }

        [Test]
        public void Jump_JustAfterSteppingOffAnEdge_StillCounts()
        {
            var locomotion = new PlayerLocomotion(Settings);
            locomotion.Step(PlayerCommands.None, 0f, new MotorContact(true, false), Frame);
            locomotion.Step(PlayerCommands.None, 0f, new MotorContact(false, false), Frame);

            locomotion.Step(Jump, 0f, new MotorContact(false, false), Frame);

            Assert.That(locomotion.Velocity.Y, Is.EqualTo(Settings.JumpSpeed));
        }

        [Test]
        public void Jump_LongAfterLeavingTheGround_DoesNothing()
        {
            var locomotion = new PlayerLocomotion(Settings);
            locomotion.Step(PlayerCommands.None, 0f, new MotorContact(true, false), Frame);
            for (var t = 0f; t < Settings.CoyoteTime + 0.05f; t += Frame)
                locomotion.Step(PlayerCommands.None, 0f, new MotorContact(false, false), Frame);

            locomotion.Step(Jump, 0f, new MotorContact(false, false), Frame);

            Assert.That(locomotion.Velocity.Y, Is.LessThan(0f));
        }

        [Test]
        public void Falling_SpeedsUp_ToTheLimit()
        {
            var locomotion = new PlayerLocomotion(Settings);

            locomotion.Step(PlayerCommands.None, 0f, new MotorContact(false, false), 0.5f);
            Assert.That(locomotion.Velocity.Y, Is.EqualTo(-Settings.Gravity * 0.5f).Within(1e-3f));

            for (var i = 0; i < 20; i++)
                locomotion.Step(PlayerCommands.None, 0f, new MotorContact(false, false), 0.5f);
            Assert.That(locomotion.Velocity.Y, Is.EqualTo(-Settings.MaxFallSpeed));
        }

        [Test]
        public void AHeadAgainstACeiling_StopsRising()
        {
            var player = OnFlatGround(new FlatGround(ceiling: 0.3f));

            player.Tick(Jump, Frame);
            Run(player, PlayerCommands.None, 0.1f);

            Assert.That(player.Locomotion.Velocity.Y, Is.LessThanOrEqualTo(0f));
            Assert.That(player.Feet.Y, Is.LessThanOrEqualTo(0.3f));
        }

        [Test]
        public void AirSteering_IsWeakerThanOnTheGround()
        {
            var locomotion = new PlayerLocomotion(Settings);

            locomotion.Step(Walk(0f, 1f), 0f, new MotorContact(false, false), 0.1f);

            Assert.That(locomotion.Velocity.Z, Is.EqualTo(Settings.AirAcceleration * 0.1f).Within(1e-4f));
        }

        [Test]
        public void NoTime_NoMove()
        {
            var locomotion = new PlayerLocomotion(Settings);

            Assert.That(locomotion.Step(Walk(0f, 1f), 0f, new MotorContact(true, false), 0f), Is.EqualTo(Vector3.Zero));
            Assert.That(locomotion.Step(Walk(0f, 1f), 0f, new MotorContact(true, false), float.NaN), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void PlaceAt_StandsStill_FacingTheHeading()
        {
            var player = OnFlatGround();
            Run(player, Walk(0f, 1f), 0.5f);
            player.Look.Set(10f, 30f);

            player.PlaceAt(new Vector3(5f, 2f, -3f), 270f);

            Assert.That(player.Feet, Is.EqualTo(new Vector3(5f, 2f, -3f)));
            Assert.That(Vector3.Distance(player.Eyes, new Vector3(5f, 3.65f, -3f)), Is.LessThan(1e-5f));
            Assert.That(player.Look.Heading, Is.EqualTo(270f));
            Assert.That(player.Look.Pitch, Is.Zero);
            Assert.That(player.Locomotion.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(player.IsGrounded, Is.False, "It finds the ground on its next move.");
        }

        [Test]
        public void Settings_RefuseNonsense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocomotionSettings(runSpeed: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocomotionSettings(gravity: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocomotionSettings(jumpHeight: -1f));
            Assert.That(new LocomotionSettings(gravity: 20f, jumpHeight: 1.25f).JumpSpeed, Is.EqualTo(Math.Sqrt(50.0)).Within(1e-4f));
        }
    }
}
