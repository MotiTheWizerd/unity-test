using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Game.Actors;
using Forge.Game.Combat;
using Forge.Game.Events;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class FightCausesTests
    {
        static readonly ActorId Dummy = new ActorId("dummy");
        static readonly RecordId Sword = new RecordId("forge.core", "sword");

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
            FightCauses.Bind(_modes, _events);
        }

        [Test]
        public void ABlowThatLands_PutsBothInAFight_AgainstEachOther()
        {
            _events.Publish(new HitLanded(ActorId.Player, Dummy, Sword, 12f));
            _events.Flush();

            Assert.That(_modes.Fights(ActorId.Player, Dummy), Is.True);
            Assert.That(_modes.Fights(Dummy, ActorId.Player), Is.True);
            Assert.That(_told.Select(e => e.ToString()), Is.EqualTo(new[] { "player: Normal → Fight", "dummy: Normal → Fight" }));
        }

        [Test]
        public void ABlowThatIsBlocked_StartsTheFightToo()
        {
            _events.Publish(new HitBlocked(Dummy, ActorId.Player, Sword, 7f));
            _events.Flush();

            Assert.That(_modes.Fights(ActorId.Player, Dummy), Is.True);
            Assert.That(_modes.Fights(Dummy, ActorId.Player), Is.True);
        }

        [Test]
        public void AKill_EndsTheFallensFightAtOnce_AndTheKillerCoolsOff()
        {
            _events.Publish(new HitLanded(ActorId.Player, Dummy, Sword, 12f));
            _events.Publish(new ActorKilled(Dummy, ActorId.Player));
            _events.Flush();

            Assert.That(_modes.ModeOf(Dummy), Is.EqualTo(Mode.Normal));
            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Fight));
            Assert.That(_modes.OpponentsOf(ActorId.Player), Is.Empty);

            _modes.Tick(2f);
            _events.Flush();
            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Normal));
            Assert.That(_told.Select(e => e.ToString()), Is.EqualTo(new[]
            {
                "player: Normal → Fight", "dummy: Normal → Fight", "dummy: Fight → Normal", "player: Fight → Normal",
            }));
        }

        [Test]
        public void ABlowAtOneself_StartsNothing()
        {
            _events.Publish(new HitLanded(ActorId.Player, ActorId.Player, Sword, 1f));
            _events.Flush();

            Assert.That(_modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Normal));
        }

        [Test]
        public void Disposed_HearsNoMore()
        {
            var events = new GameEvents();
            var modes = new ActorModes(events);
            FightCauses.Bind(modes, events).Dispose();

            events.Publish(new HitLanded(ActorId.Player, Dummy, Sword, 12f));
            events.Flush();

            Assert.That(modes.ModeOf(ActorId.Player), Is.EqualTo(Mode.Normal));
        }
    }
}
