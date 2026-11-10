using System.Collections.Generic;
using System.Numerics;
using Forge.Game.Actors;
using Forge.Game.Events;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class CrouchTests
    {
        const float Frame = 1f / 60f;
        const float EyeHeight = 1.65f;

        static readonly LocomotionSettings Settings = LocomotionSettings.Default;

        // A body on endless flat ground at height 0 that crouches 0.6 m lower, under a low roof or not.
        sealed class CrouchingGround : IPlayerMotor, ICrouchingBody
        {
            public bool LowRoof;

            public Vector3 Position { get; private set; }

            public MotorContact Move(Vector3 displacement)
            {
                var next = Position + displacement;
                var grounded = next.Y <= 0f;
                if (grounded)
                    next.Y = 0f;
                Position = next;
                return new MotorContact(grounded, hitCeiling: false);
            }

            public void Teleport(Vector3 feet) => Position = feet;

            public bool Crouched { get; private set; }

            public float CrouchDepth => 0.6f;

            public bool CanStand => !LowRoof;

            public void Crouch(bool crouched) => Crouched = crouched;
        }

        // A body that can't crouch.
        sealed class FlatGround : IPlayerMotor
        {
            public Vector3 Position { get; private set; }

            public MotorContact Move(Vector3 displacement)
            {
                var next = Position + displacement;
                next.Y = System.Math.Max(next.Y, 0f);
                Position = next;
                return new MotorContact(next.Y <= 0f, hitCeiling: false);
            }

            public void Teleport(Vector3 feet) => Position = feet;
        }

        static PlayerCommands Crouch => new PlayerCommands(Vector2.Zero, Vector2.Zero, false, false, toggleCrouch: true);

        static PlayerCommands Forward(bool sprint = false) => new PlayerCommands(new Vector2(0f, 1f), Vector2.Zero, sprint, false);

        static PlayerCommands Jump => new PlayerCommands(Vector2.Zero, Vector2.Zero, false, true);

        static PlayerCharacter Standing(IPlayerMotor body)
        {
            var player = new PlayerCharacter(body, Settings, EyeHeight);
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
        public void TheCrouchKey_CrouchesTheBody_AndPressedAgain_StandsItUp()
        {
            var body = new CrouchingGround();
            var player = Standing(body);

            player.Tick(Crouch, Frame);
            Assert.That(player.Locomotion.Crouching, Is.True);
            Assert.That(body.Crouched, Is.True);
            Assert.That(player.Crouched, Is.True);
            Assert.That(player.Motion.Crouched, Is.True, "The look is told, to play its crouched moves.");

            player.Tick(Crouch, Frame);
            Assert.That(body.Crouched, Is.False);
            Assert.That(player.Motion.Crouched, Is.False);
        }

        [Test]
        public void Crouched_ItMovesAtTheCrouchPace()
        {
            var player = Standing(new CrouchingGround());
            player.Tick(Crouch, Frame);

            Run(player, Forward(), 1f);

            Assert.That(GroundSpeed(player), Is.EqualTo(Settings.CrouchSpeed).Within(1e-3f));
        }

        [Test]
        public void ASprint_StandsItUpToRun()
        {
            var body = new CrouchingGround();
            var player = Standing(body);
            player.Tick(Crouch, Frame);

            Run(player, Forward(sprint: true), 1f);

            Assert.That(body.Crouched, Is.False);
            Assert.That(GroundSpeed(player), Is.EqualTo(Settings.SprintSpeed).Within(1e-3f));
        }

        [Test]
        public void AJump_StandsItUp()
        {
            var body = new CrouchingGround();
            var player = Standing(body);
            player.Tick(Crouch, Frame);

            player.Tick(Jump, Frame);

            Assert.That(player.Locomotion.Jumped, Is.True);
            Assert.That(body.Crouched, Is.False);
        }

        [Test]
        public void UnderALowRoof_ItStaysDown_NeitherStandingNorSprintingNorJumping()
        {
            var body = new CrouchingGround();
            var player = Standing(body);
            player.Tick(Crouch, Frame);
            body.LowRoof = true;

            player.Tick(Crouch, Frame);
            player.Tick(Jump, Frame);
            Run(player, Forward(sprint: true), 0.5f);

            Assert.That(body.Crouched, Is.True);
            Assert.That(player.Locomotion.Jumped, Is.False);
            Assert.That(GroundSpeed(player), Is.EqualTo(Settings.CrouchSpeed).Within(1e-3f));

            body.LowRoof = false;
            player.Tick(Crouch, Frame);
            Assert.That(body.Crouched, Is.False, "Out from under it, it stands.");
        }

        [Test]
        public void TheEyes_EaseDownToTheCrouch_AndBackUp()
        {
            var player = Standing(new CrouchingGround());

            player.Tick(Crouch, Frame);
            Assert.That(player.Lowered, Is.GreaterThan(0f).And.LessThan(0.6f), "On the way down...");
            Run(player, PlayerCommands.None, 0.5f);
            Assert.That(player.Lowered, Is.EqualTo(0.6f).Within(1e-4f), "...down by the crouch's depth.");
            Assert.That(player.Eyes.Y, Is.EqualTo(EyeHeight - 0.6f).Within(1e-4f));

            player.Tick(Crouch, Frame);
            Run(player, PlayerCommands.None, 0.5f);
            Assert.That(player.Lowered, Is.EqualTo(0f));
        }

        [Test]
        public void TakingOff_IsToldOnTheStream_OnceAJump()
        {
            var events = new GameEvents();
            var jumped = new List<ActorJumped>();
            events.Subscribe<ActorJumped>(jumped.Add);
            var player = new PlayerCharacter(new CrouchingGround(), Settings, EyeHeight, events: events);
            player.Tick(PlayerCommands.None, Frame);

            player.Tick(Jump, Frame);
            player.Tick(Jump, Frame);
            events.Flush();

            Assert.That(jumped.Count, Is.EqualTo(1), "In the air, a second press is no jump.");
            Assert.That(jumped[0].Actor, Is.EqualTo(ActorId.Player));
        }

        [Test]
        public void ABodyThatCantCrouch_NeverDoes()
        {
            var player = Standing(new FlatGround());

            player.Tick(Crouch, Frame);

            Assert.That(player.Locomotion.Crouching, Is.False);
            Assert.That(player.Crouched, Is.False);
            Assert.That(player.Lowered, Is.EqualTo(0f));
        }
    }
}
