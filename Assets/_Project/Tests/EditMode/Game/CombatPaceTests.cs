using System;
using System.Numerics;
using Forge.Game.Actors;
using Forge.Game.Events;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class CombatPaceTests
    {
        static readonly ActorId Bandit = new ActorId("bandit");
        static readonly Vector2 Ahead = new Vector2(0f, 1f);

        GameEvents _events;
        ActorModes _modes;
        CombatPace _pace;

        [SetUp]
        public void SetUp()
        {
            _events = new GameEvents();
            _modes = new ActorModes(_events, coolOff: 1f);
            _pace = new CombatPace(_modes, ActorId.Player);
        }

        static PlayerCommands Told(bool sprint, bool walk = false) =>
            new PlayerCommands(Ahead, Vector2.Zero, sprint, jump: true, walk: walk);

        [Test]
        public void AtEase_TheCommandsPassUntouched()
        {
            Assert.That(_pace.InAFight, Is.False);

            var run = _pace.Allow(Told(sprint: false));
            Assert.That(run.Walk, Is.False, "The everyday run.");
            Assert.That(run.Sprint, Is.False);

            var sprint = _pace.Allow(Told(sprint: true));
            Assert.That(sprint.Sprint, Is.True, "A sprint when the key is held.");
            Assert.That(sprint.Walk, Is.False);
        }

        [Test]
        public void InAFight_ThePlayerWalks_UnlessTheRunKeyIsHeld_WhichRunsRatherThanSprints()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _events.Flush();
            Assert.That(_pace.InAFight, Is.True);

            var walk = _pace.Allow(Told(sprint: false));
            Assert.That(walk.Walk, Is.True, "Not running by default.");
            Assert.That(walk.Move, Is.EqualTo(Ahead), "The move itself is untouched...");
            Assert.That(walk.Jump, Is.True, "...and it may still jump.");

            var run = _pace.Allow(Told(sprint: true));
            Assert.That(run.Walk, Is.False, "The run key runs...");
            Assert.That(run.Sprint, Is.False, "...and no faster.");
        }

        [Test]
        public void InAFight_AWalkForcedAlready_StaysOne_RunKeyOrNot()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _events.Flush();

            var laden = _pace.Allow(Told(sprint: true, walk: true));
            Assert.That(laden.Walk, Is.True, "Too laden to run before the fight: still too laden.");
            Assert.That(laden.Sprint, Is.False);
        }

        [Test]
        public void TheFightOver_ThePlayerRunsAgain_OnceItHasCooledOff()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _modes.Fallen(Bandit);
            _events.Flush();
            Assert.That(_pace.InAFight, Is.True, "The foe down, the player is still on edge for the cool-off...");
            Assert.That(_pace.Allow(Told(sprint: false)).Walk, Is.True);

            _modes.Tick(1f);
            _events.Flush();
            Assert.That(_pace.InAFight, Is.False, "...then at ease.");
            Assert.That(_pace.Allow(Told(sprint: false)).Walk, Is.False);
        }

        [Test]
        public void ItNeedsTheModes_AndAnActor()
        {
            Assert.Throws<ArgumentNullException>(() => new CombatPace(null, ActorId.Player));
            Assert.Throws<ArgumentException>(() => new CombatPace(_modes, default));
        }
    }
}
