using System.Numerics;
using Forge.Game.Actors;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class ActorCommandsTests
    {
        static readonly PlayerCommands Ahead = new PlayerCommands(new Vector2(0f, 1f), Vector2.Zero, sprint: false, jump: false);

        [Test]
        public void None_StandsStill_HandsIdle()
        {
            var none = ActorCommands.None;

            Assert.That(none.Move.Move, Is.EqualTo(Vector2.Zero));
            Assert.That(none.ReadyWeapon, Is.False);
            Assert.That(none.Uses(right: true), Is.False);
            Assert.That(none.Uses(right: false), Is.False);
            Assert.That(none.Releases(right: true), Is.False);
            Assert.That(none.Releases(right: false), Is.False);
        }

        [Test]
        public void Moving_CarriesTheMove_AndNothingElse()
        {
            var moving = ActorCommands.Moving(Ahead);

            Assert.That(moving.Move.Move, Is.EqualTo(Ahead.Move));
            Assert.That(moving.ReadyWeapon, Is.False);
            Assert.That(moving.UseRight | moving.UseLeft | moving.ReleaseRight | moving.ReleaseLeft, Is.False);
        }

        [Test]
        public void EachHand_IsAskedForByItsSide()
        {
            var commands = new ActorCommands(Ahead, readyWeapon: true, useRight: true, releaseLeft: true);

            Assert.That(commands.ReadyWeapon, Is.True);
            Assert.That(commands.Uses(right: true), Is.True);
            Assert.That(commands.Uses(right: false), Is.False);
            Assert.That(commands.Releases(right: true), Is.False);
            Assert.That(commands.Releases(right: false), Is.True);
        }
    }
}
