using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Game.Actors;
using Forge.Game.Events;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class ActorModesTests
    {
        static readonly ActorId Bandit = new ActorId("bandit");
        static readonly ActorId Wolf = new ActorId("wolf");

        GameEvents _events;
        ActorModes _modes;
        List<ModeChanged> _told;

        [SetUp]
        public void SetUp()
        {
            _events = new GameEvents();
            _modes = new ActorModes(_events, coolOff: 2f);
            _told = new List<ModeChanged>();
            _events.Subscribe<ModeChanged>(_told.Add);
        }

        [Test]
        public void EveryoneIsAtEase_UntilSomethingHappens()
        {
            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Normal));
            Assert.That(_modes.OpponentsOf(ActorId.Player), Is.Empty);
            Assert.That(_modes.Fights(ActorId.Player, Bandit), Is.False);
        }

        [Test]
        public void Fight_EntersFightAgainstTheOpponent_ToldOnce()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _modes.Fight(ActorId.Player, Wolf);
            _events.Flush();

            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Fight));
            Assert.That(_modes.OpponentsOf(ActorId.Player), Is.EquivalentTo(new[] { Bandit, Wolf }));
            Assert.That(_modes.Fights(ActorId.Player, Bandit), Is.True);
            Assert.That(_told.Select(e => e.ToString()), Is.EqualTo(new[] { "player: Normal → Fight" }));
        }

        [Test]
        public void Fight_IsOneWay_TheOpponentHasItsOwnCause()
        {
            _modes.Fight(ActorId.Player, Bandit);

            Assert.That(_modes.ModeOf(Bandit), Is.EqualTo(Mode.Normal));
            Assert.That(_modes.Fights(Bandit, ActorId.Player), Is.False);
        }

        [Test]
        public void Fight_WithItself_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => _modes.Fight(ActorId.Player, ActorId.Player));
            Assert.Throws<ArgumentException>(() => _modes.Fight(default, Bandit));
        }

        [Test]
        public void Settle_TheLastOpponent_CoolsOff_ThenAtEase()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _modes.Fight(ActorId.Player, Wolf);
            _modes.Settle(ActorId.Player, Bandit);
            _modes.Tick(10f);
            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Fight), "one opponent left: no cool-off");

            _modes.Settle(ActorId.Player, Wolf);
            _modes.Tick(1.5f);
            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Fight), "cooling off");
            Assert.That(_modes.OpponentsOf(ActorId.Player), Is.Empty);

            _modes.Tick(0.6f);
            _events.Flush();
            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Normal));
            Assert.That(_told.Select(e => e.ToString()), Is.EqualTo(new[] { "player: Normal → Fight", "player: Fight → Normal" }));
        }

        [Test]
        public void Settle_WhatItWasNotFighting_DoesNothing()
        {
            _modes.Settle(ActorId.Player, Bandit);
            _modes.Fight(ActorId.Player, Bandit);
            _modes.Settle(ActorId.Player, Wolf);
            _modes.Tick(10f);

            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Fight));
            Assert.That(_modes.Fights(ActorId.Player, Bandit), Is.True);
        }

        [Test]
        public void Fight_WhileCoolingOff_CallsTheCoolOffOff_WithoutAChange()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _modes.Settle(ActorId.Player, Bandit);
            _modes.Tick(1.9f);
            _modes.Fight(ActorId.Player, Wolf);
            _modes.Tick(10f);
            _events.Flush();

            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Fight));
            Assert.That(_told, Has.Count.EqualTo(1));
        }

        [Test]
        public void Fallen_EndsItsOwnFightAtOnce_AndSettlesWhoeverFoughtIt()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _modes.Fight(Bandit, ActorId.Player);
            _modes.Fight(Wolf, Bandit);
            _modes.Fight(Wolf, ActorId.Player);
            _events.Flush();
            _told.Clear();

            _modes.Fallen(Bandit);
            _events.Flush();

            Assert.That(_modes.ModeOf(Bandit), Is.EqualTo(Mode.Normal));
            Assert.That(_told.Select(e => e.ToString()), Is.EqualTo(new[] { "bandit: Fight → Normal" }));
            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Fight), "cooling off");
            Assert.That(_modes.OpponentsOf(ActorId.Player), Is.Empty);
            Assert.That(_modes.OpponentsOf(Wolf), Is.EquivalentTo(new[] { ActorId.Player }), "still fighting the player");

            _modes.Tick(2f);
            _events.Flush();
            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Normal));
            Assert.That(_modes.ModeOf(Wolf), Is.EqualTo(Mode.Fight));
        }

        [Test]
        public void Fallen_WhenNoOneFoughtIt_TellsNothing()
        {
            _modes.Fallen(Bandit);
            _events.Flush();

            Assert.That(_told, Is.Empty);
        }

        [Test]
        public void Clear_PutsEveryoneAtEase_Told()
        {
            _modes.Fight(ActorId.Player, Bandit);
            _modes.Fight(Bandit, ActorId.Player);
            _events.Flush();
            _told.Clear();

            _modes.Clear();
            _events.Flush();

            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Normal));
            Assert.That(_modes.ModeOf(Bandit), Is.EqualTo(Mode.Normal));
            Assert.That(_told.Select(e => e.To), Is.All.EqualTo(Mode.Normal));
            Assert.That(_told.Select(e => e.Actor), Is.EquivalentTo(new[] { ActorId.Player, Bandit }));
        }

        [Test]
        public void ACoolOffOfZero_IsAtEaseOnTheNextTick()
        {
            var modes = new ActorModes(coolOff: 0f);
            modes.Fight(ActorId.Player, Bandit);
            modes.Settle(ActorId.Player, Bandit);
            Assert.That(modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Fight));

            modes.Tick(0f);
            Assert.That(modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Normal));
        }

        [Test]
        public void ANegativeCoolOff_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActorModes(coolOff: -1f));
        }
    }
}
