using System.Numerics;
using Forge.Game.Motion;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class BodyFacingTests
    {
        const float Frame = 1f / 60f;

        sealed class FlatGround : IMotor
        {
            public Vector3 Position { get; private set; }

            public MotorContact Move(Vector3 displacement)
            {
                var next = Position + displacement;
                var grounded = next.Y <= 0f;
                if (grounded)
                    next.Y = 0f;
                Position = next;
                return new MotorContact(grounded, false);
            }

            public void Teleport(Vector3 feet) => Position = feet;
        }

        static PlayerCharacter Standing(BodyFacing facing)
        {
            var player = new PlayerCharacter(new FlatGround(), LocomotionSettings.Default, 1.65f) { Facing = facing };
            player.PlaceAt(Vector3.Zero, 0f);
            // One still frame, so it knows it stands.
            player.Tick(PlayerCommands.None, Frame);
            return player;
        }

        static void Run(PlayerCharacter player, PlayerCommands commands, float seconds)
        {
            for (var t = 0f; t < seconds; t += Frame)
                player.Tick(commands, Frame);
        }

        static PlayerCommands Turn(float degrees) => new PlayerCommands(Vector2.Zero, new Vector2(degrees, 0f), false, false);

        static PlayerCommands Walk(float x, float y) => new PlayerCommands(new Vector2(x, y), Vector2.Zero, false, false);

        [Test]
        public void FacingTheLook_TheBodyTurnsWithIt()
        {
            var player = Standing(BodyFacing.Look);

            player.Tick(Turn(70f), Frame);

            Assert.That(player.BodyHeading, Is.EqualTo(70f).Within(1e-4f));
        }

        [Test]
        public void FacingMovement_StandingStill_TheLookOrbitsAlone()
        {
            var player = Standing(BodyFacing.Movement);

            player.Tick(Turn(120f), Frame);

            Assert.That(player.Look.Heading, Is.EqualTo(120f).Within(1e-4f));
            Assert.That(player.BodyHeading, Is.Zero, "Standing, the body stays as it was.");
        }

        [Test]
        public void FacingMovement_TheBodyTurnsTowardWhereItGoes_AtTheTurnRate()
        {
            var player = Standing(BodyFacing.Movement);

            // Looking north, stepping right: it runs east.
            player.Tick(Walk(1f, 0f), Frame);
            var firstStep = player.BodyHeading;
            Run(player, Walk(1f, 0f), 0.5f);

            Assert.That(firstStep, Is.EqualTo(LocomotionSettings.Default.TurnRate * Frame).Within(1e-3f), "Turns, not snaps.");
            Assert.That(player.BodyHeading, Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void Aiming_TheBodyTurnsToWhereItLooks_AtTheTurnRate_EvenStandingAndFacingMovement()
        {
            var player = Standing(BodyFacing.Movement);
            player.Tick(Turn(150f), Frame);
            Assert.That(player.BodyHeading, Is.Zero, "Not aiming: standing, it stays.");

            player.Aiming = true;
            player.Tick(PlayerCommands.None, Frame);
            var firstStep = player.BodyHeading;
            Run(player, PlayerCommands.None, 1f);

            Assert.That(firstStep, Is.EqualTo(LocomotionSettings.Default.TurnRate * Frame).Within(1e-3f), "Turns, not snaps.");
            Assert.That(player.BodyHeading, Is.EqualTo(150f).Within(1e-3f));
        }

        [Test]
        public void FacingTarget_TheBodyTurnsToItsTargetHeading_AtTheTurnRate_StandingOrMoving_WhereverItLooks()
        {
            var player = Standing(BodyFacing.Target);
            player.TargetHeading = 150f;

            player.Tick(Turn(40f), Frame);
            var firstStep = player.BodyHeading;
            Run(player, PlayerCommands.None, 1f);
            var standing = player.BodyHeading;
            // Stepping left while looking at 40: the body keeps facing its target, not the look nor the way it goes.
            Run(player, Walk(-1f, 0f), 0.5f);

            Assert.That(firstStep, Is.EqualTo(LocomotionSettings.Default.TurnRate * Frame).Within(1e-3f), "Turns, not snaps.");
            Assert.That(standing, Is.EqualTo(150f).Within(1e-3f));
            Assert.That(player.BodyHeading, Is.EqualTo(150f).Within(1e-3f));
            Assert.That(player.Look.Heading, Is.EqualTo(40f).Within(1e-3f), "The look goes its own way.");
        }

        [Test]
        public void FacingTarget_ADrawnBowSquaresUpToTheLookInstead()
        {
            var player = Standing(BodyFacing.Target);
            player.TargetHeading = 150f;
            player.Tick(Turn(60f), Frame);

            player.Aiming = true;
            Run(player, PlayerCommands.None, 1f);

            Assert.That(player.BodyHeading, Is.EqualTo(60f).Within(1e-3f));
        }

        [Test]
        public void Placed_ItsTargetIsTheWayItFaces()
        {
            var player = Standing(BodyFacing.Target);

            player.PlaceAt(Vector3.Zero, 200f);
            Run(player, PlayerCommands.None, 0.5f);

            Assert.That(player.TargetHeading, Is.EqualTo(200f).Within(1e-3f));
            Assert.That(player.BodyHeading, Is.EqualTo(200f).Within(1e-3f), "No turn back to wherever its last target was.");
        }

        [Test]
        public void FacingMovement_TurnsTheShortWayRound()
        {
            var player = Standing(BodyFacing.Movement);

            // Stepping left from north is west: through 359, not round through east.
            player.Tick(Walk(-1f, 0f), Frame);

            Assert.That(player.BodyHeading, Is.GreaterThan(300f));
        }
    }
}
