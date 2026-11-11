using System.Collections.Generic;
using System.Numerics;
using Forge.Game.Actors;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class OpponentsTests
    {
        static readonly ActorId Bandit = new ActorId("bandit");
        static readonly ActorId Wolf = new ActorId("wolf");

        sealed class Standing : IStandingActors
        {
            public readonly Dictionary<ActorId, Vector3> Feet = new Dictionary<ActorId, Vector3>();

            public Vector3? FeetOf(ActorId actor) => Feet.TryGetValue(actor, out var feet) ? feet : (Vector3?)null;
        }

        ActorModes _modes;
        Standing _standing;

        [SetUp]
        public void SetUp()
        {
            _modes = new ActorModes();
            _standing = new Standing();
            _standing.Feet[Bandit] = new Vector3(5f, 0f, 0f);
            _standing.Feet[Wolf] = new Vector3(0f, 0f, -2f);
        }

        bool Nearest(out ActorId foe, out Vector3 feet) => Opponents.TryNearest(_modes, ActorId.Player, Vector3.Zero, _standing, out foe, out feet);

        [Test]
        public void AtEase_ThereIsNoOne()
        {
            Assert.That(Nearest(out var foe, out _), Is.False);
            Assert.That(foe.IsDefault, Is.True);
        }

        [Test]
        public void TheNearestOpponentThatStands_AndWhereItsFeetAre()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _modes.Fight(ActorId.Player, Wolf);

            Assert.That(Nearest(out var foe, out var feet), Is.True);
            Assert.That(foe, Is.EqualTo(Wolf), "2 m beats 5 m.");
            Assert.That(feet, Is.EqualTo(new Vector3(0f, 0f, -2f)));
        }

        [Test]
        public void AnOpponentThatDoesNotStand_IsPassedOver()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _modes.Fight(ActorId.Player, Wolf);
            _standing.Feet.Remove(Wolf);

            Assert.That(Nearest(out var foe, out _), Is.True);
            Assert.That(foe, Is.EqualTo(Bandit), "The wolf lies fallen (or is gone): the bandit is next.");
        }

        [Test]
        public void CoolingOff_ThereIsNoOne()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _modes.Settle(ActorId.Player, Bandit);

            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Fight), "Still in the fight's cool-off.");
            Assert.That(Nearest(out _, out _), Is.False);
        }

        [Test]
        public void OnlyItsOwnOpponents()
        {
            _modes.Fight(Wolf, Bandit);

            Assert.That(Nearest(out _, out _), Is.False, "The wolf fights the bandit; the player fights no one.");
        }
    }
}
