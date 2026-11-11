using System.Collections.Generic;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Actors;
using Forge.Game.Equipment;
using Forge.Game.Motion;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class SoftLockOnTests
    {
        const float Frame = 1f / 60f;

        static readonly ActorId Bandit = new ActorId("bandit");
        static readonly ActorId Wolf = new ActorId("wolf");

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

        sealed class Standing : IStandingActors
        {
            public readonly Dictionary<ActorId, Vector3> Feet = new Dictionary<ActorId, Vector3>();

            public Vector3? FeetOf(ActorId actor) => Feet.TryGetValue(actor, out var feet) ? feet : (Vector3?)null;
        }

        ActorModes _modes;
        Standing _standing;
        SoftLockOn _lockOn;
        PlayerCharacter _player;

        [SetUp]
        public void SetUp()
        {
            _modes = new ActorModes();
            _standing = new Standing();
            _lockOn = new SoftLockOn(_modes, ActorId.Player);
            _player = new PlayerCharacter(new FlatGround(), LocomotionSettings.Default, 1.65f);
            _player.PlaceAt(Vector3.Zero, 0f);
        }

        // Third person (the view turns the body to its movement), weapons in hand, unless said otherwise.
        void Face(BodyFacing view = BodyFacing.Movement, Readiness readiness = Readiness.Drawn) =>
            _lockOn.Face(_player, view, readiness, _standing);

        void Fighting(ActorId foe, Vector3 feet)
        {
            _modes.Fight(ActorId.Player, foe);
            _standing.Feet[foe] = feet;
        }

        [Test]
        public void AtEase_TheBodyFacesAsTheViewSays()
        {
            _standing.Feet[Bandit] = new Vector3(3f, 0f, 0f);

            Face();

            Assert.That(_player.Facing, Is.EqualTo(BodyFacing.Movement));
            Assert.That(_lockOn.Foe.IsDefault, Is.True);
        }

        [Test]
        public void InAFight_WeaponsOut_TheBodySquaresUpToItsFoe()
        {
            Fighting(Bandit, new Vector3(3f, 0f, 0f));

            Face();

            Assert.That(_player.Facing, Is.EqualTo(BodyFacing.Target));
            Assert.That(_player.TargetHeading, Is.EqualTo(90f).Within(1e-3f), "The bandit stands east.");
            Assert.That(_lockOn.Foe, Is.EqualTo(Bandit));
        }

        [Test]
        public void ItSquaresUpToTheNearestFoe()
        {
            Fighting(Bandit, new Vector3(5f, 0f, 0f));
            Fighting(Wolf, new Vector3(0f, 0f, -2f));

            Face();

            Assert.That(_lockOn.Foe, Is.EqualTo(Wolf));
            Assert.That(_player.TargetHeading, Is.EqualTo(180f).Within(1e-3f), "The wolf stands south, nearer.");
        }

        [Test]
        public void WeaponsComingOut_AreOut_WeaponsGoingAway_AreNot()
        {
            Fighting(Bandit, new Vector3(3f, 0f, 0f));

            Face(readiness: Readiness.Drawing);
            Assert.That(_player.Facing, Is.EqualTo(BodyFacing.Target), "Drawing: the drawn moves play from the draw on.");

            Face(readiness: Readiness.Sheathing);
            Assert.That(_player.Facing, Is.EqualTo(BodyFacing.Movement));

            Face(readiness: Readiness.Sheathed);
            Assert.That(_player.Facing, Is.EqualTo(BodyFacing.Movement), "Weapons away, it goes the way it goes.");
            Assert.That(_lockOn.Foe.IsDefault, Is.True);
        }

        [Test]
        public void AFoeOutOfRange_IsLetGo()
        {
            Fighting(Bandit, new Vector3(0f, 0f, SoftLockOn.DefaultRange - 0.5f));
            Face();
            Assert.That(_player.Facing, Is.EqualTo(BodyFacing.Target), "Within range.");

            _standing.Feet[Bandit] = new Vector3(0f, 0f, SoftLockOn.DefaultRange + 0.5f);
            Face();
            Assert.That(_player.Facing, Is.EqualTo(BodyFacing.Movement), "Out of range: the player got away, or the foe did.");
            Assert.That(_lockOn.Foe.IsDefault, Is.True);
        }

        [Test]
        public void AFoeThatNoLongerStands_IsLetGo()
        {
            Fighting(Bandit, new Vector3(3f, 0f, 0f));
            _standing.Feet.Remove(Bandit);

            Face();

            Assert.That(_player.Facing, Is.EqualTo(BodyFacing.Movement));
        }

        [Test]
        public void InFirstPerson_TheBodyIsTheLook()
        {
            Fighting(Bandit, new Vector3(3f, 0f, 0f));

            Face(BodyFacing.Look);

            Assert.That(_player.Facing, Is.EqualTo(BodyFacing.Look));
            Assert.That(_lockOn.Foe.IsDefault, Is.True);
        }

        [Test]
        public void Locked_StepsGoTheCamerasWay_WhileTheBodyKeepsFacingItsFoe()
        {
            var foe = new Vector3(0f, 0f, 4f);
            Fighting(Bandit, foe);
            // The camera turns east, side-on to the bandit north; stepping ahead then goes east: a sidestep.
            _player.Tick(new PlayerCommands(Vector2.Zero, new Vector2(90f, 0f), false, false), Frame);
            var ahead = new PlayerCommands(new Vector2(0f, 1f), Vector2.Zero, false, false);
            for (var t = 0f; t < 0.5f; t += Frame)
            {
                Face();
                _player.Tick(ahead, Frame);
            }

            var feet = _player.Feet;
            var towardFoe = Compass.HeadingOf(new Vector2(foe.X - feet.X, foe.Z - feet.Z));
            Assert.That(feet.X, Is.GreaterThan(0.5f), "It went east, the camera's way.");
            Assert.That(_player.Look.Heading, Is.EqualTo(90f).Within(1e-3f), "The camera stays the mouse's.");
            var off = Compass.Normalize(_player.BodyHeading - towardFoe + 180f) - 180f;
            Assert.That(off, Is.EqualTo(0f).Within(2f), "The body faces the bandit as it goes past.");
        }
    }
}
