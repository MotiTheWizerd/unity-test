using System;
using System.Numerics;
using Forge.Game.Actors;
using Forge.Game.Minds;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class GazeTests
    {
        const float Frame = 1f / 60f;
        const float EyeLevel = 1.6f;

        static readonly ActorId Bandit = new ActorId("bandit");
        static readonly GazeSettings Settings = GazeSettings.Person;

        // Nothing stands between any two points until a wall is put up.
        sealed class Sight : ILineOfSight
        {
            public bool Walled;

            public bool Clear(Vector3 from, Vector3 to) => !Walled;
        }

        Sight _sight;
        Gaze _gaze;

        [SetUp]
        public void SetUp()
        {
            _sight = new Sight();
            _gaze = new Gaze(Settings, _sight);
        }

        // Someone standing at (x, z), their eyes as high as its own.
        static GazeInterest At(ActorId who, float x, float z) => new GazeInterest(who, new Vector3(x, EyeLevel, z));

        // It looks about from the origin, facing north, for `seconds`, a frame at a time; whom it looks at then.
        ActorId Look(float seconds, params GazeInterest[] interests)
        {
            var eyes = new Vector3(0f, EyeLevel, 0f);
            for (var t = 0f; t < seconds; t += Frame)
                _gaze.Look(eyes, 0f, interests, Frame);
            return _gaze.Target;
        }

        ActorId Look(params GazeInterest[] interests) => Look(Frame, interests);

        [Test]
        public void SomeoneNearBeforeIt_IsLookedAt()
        {
            Assert.That(Look(At(ActorId.Player, 0.5f, 3f)), Is.EqualTo(ActorId.Player));
            Assert.That(_gaze.Looking, Is.True);
        }

        [Test]
        public void SomeoneBesideIt_IsLookedAt_SomeoneBehindIt_IsNot()
        {
            Assert.That(Look(At(ActorId.Player, 3f, 0f)), Is.EqualTo(ActorId.Player), "East, beside it as it faces north.");

            _gaze.LookAway();
            Assert.That(Look(At(ActorId.Player, 0f, -3f)).IsDefault, Is.True, "South, behind it.");
            Assert.That(Look(At(ActorId.Player, 2.5f, -1.2f)).IsDefault, Is.True, "Past its sight's edge (100°), over its shoulder.");
        }

        [Test]
        public void SomeoneFar_IsNotLookedAt()
        {
            Assert.That(Look(At(ActorId.Player, 0f, Settings.Notice + 0.5f)).IsDefault, Is.True);
        }

        [Test]
        public void SomeoneRightOverIt_IsSeenWhicheverWayItFaces()
        {
            var above = new GazeInterest(ActorId.Player, new Vector3(0f, EyeLevel + 2f, 0f));
            Assert.That(Look(above), Is.EqualTo(ActorId.Player));
        }

        [Test]
        public void SomeoneBehindAWall_IsNotLookedAt()
        {
            _sight.Walled = true;
            Assert.That(Look(At(ActorId.Player, 0f, 2f)).IsDefault, Is.True);
        }

        [Test]
        public void OnceLooking_ItKeepsOnThemOutToLetGo_ThenLooksAway()
        {
            Look(At(ActorId.Player, 0f, 3f));

            var between = (Settings.Notice + Settings.LetGo) * 0.5f;
            Assert.That(Look(1f, At(ActorId.Player, 0f, between)), Is.EqualTo(ActorId.Player),
                "Past where it noticed them, short of where it lets go: still looking, so the edge doesn't flicker.");

            var gone = At(ActorId.Player, 0f, Settings.LetGo + 0.5f);
            Assert.That(Look(Settings.Linger * 0.5f, gone), Is.EqualTo(ActorId.Player), "A moment past: it lingers.");
            Assert.That(Look(Settings.Linger, gone).IsDefault, Is.True, "Then it looks away.");
        }

        [Test]
        public void SomeoneOutOfSightAMoment_IsFollowedThrough_LongerIsLetGo()
        {
            var player = At(ActorId.Player, 0f, 3f);
            Look(player);

            _sight.Walled = true;
            Assert.That(Look(Settings.Linger * 0.5f, player), Is.EqualTo(ActorId.Player), "Behind a post a moment.");
            _sight.Walled = false;
            Assert.That(Look(player), Is.EqualTo(ActorId.Player));

            _sight.Walled = true;
            Assert.That(Look(Settings.Linger + 0.1f, player).IsDefault, Is.True, "Gone behind the wall.");
        }

        [Test]
        public void SomeoneMoreInteresting_TakesItsEye_SomeoneLess_DoesNot()
        {
            Look(At(Bandit, 1f, 3f));
            Assert.That(_gaze.Target, Is.EqualTo(Bandit));

            Assert.That(Look(At(ActorId.Player, -1f, 3f), At(Bandit, 1f, 3f)), Is.EqualTo(ActorId.Player), "The player comes first.");
            Assert.That(Look(1f, At(ActorId.Player, -1f, 3f), At(Bandit, 1f, 2f)), Is.EqualTo(ActorId.Player),
                "The bandit, nearer but less interesting, doesn't take it back.");
        }

        [Test]
        public void AFoe_IsWatchedWhereverItStands_SeenOrNot()
        {
            _sight.Walled = true;
            var foe = new GazeInterest(Bandit, new Vector3(-2f, EyeLevel, -Settings.LetGo - 3f), focused: true);

            Assert.That(Look(foe), Is.EqualTo(Bandit), "Far, behind it, past a wall: its mind knows where its foe is.");
            Assert.That(Look(Settings.Linger * 4f, foe), Is.EqualTo(Bandit), "And it never lets it go while they fight.");
        }

        [Test]
        public void AFoe_TakesItsEyeFromSomeonePassingBy()
        {
            Look(At(ActorId.Player, 0f, 2f));

            var foe = new GazeInterest(Bandit, new Vector3(0f, EyeLevel, 4.5f), focused: true);
            Assert.That(Look(foe, At(ActorId.Player, 0f, 2f)), Is.EqualTo(Bandit));
        }

        [Test]
        public void SomeoneOfNoMoreInterest_IsLetGoAtOnce()
        {
            Look(At(ActorId.Player, 0f, 3f));

            Assert.That(Look().IsDefault, Is.True, "Fallen or gone: no lingering.");
        }

        [Test]
        public void ItsSettings_MustMakeSense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GazeSettings(4f, 3f, 100f, 0.5f), "Letting go nearer than noticing.");
            Assert.Throws<ArgumentOutOfRangeException>(() => new GazeSettings(0f, 3f, 100f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GazeSettings(4f, 5f, 190f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GazeSettings(4f, 5f, 100f, -1f));
            Assert.Throws<ArgumentNullException>(() => new Gaze(Settings, null));
        }
    }
}
