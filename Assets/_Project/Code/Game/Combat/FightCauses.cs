using System;
using Forge.Game.Actors;
using Forge.Game.Events;

namespace Forge.Game.Combat
{
    /// <summary>
    /// What combat does to actors' modes (<see cref="ActorModes"/>): a blow that lands or is blocked
    /// (<see cref="HitLanded"/>, <see cref="HitBlocked"/>) puts both in <see cref="Mode.Fight"/> against each other
    /// (hitting someone starts the fight; drawing a weapon never does), and a death (<see cref="ActorKilled"/>) settles
    /// everyone with the fallen. Swinging at air starts nothing. Other causes (a foe that sees you, a crime) will bind
    /// beside it.
    /// </summary>
    public static class FightCauses
    {
        /// <summary>Has what <paramref name="events"/> tells of combat set <paramref name="modes"/> until the returned binding is disposed.</summary>
        public static IDisposable Bind(ActorModes modes, GameEvents events) => new Binding(modes, events);

        sealed class Binding : IDisposable
        {
            readonly ActorModes _modes;
            readonly IDisposable[] _subscriptions;

            public Binding(ActorModes modes, GameEvents events)
            {
                _modes = modes ?? throw new ArgumentNullException(nameof(modes));
                if (events == null)
                    throw new ArgumentNullException(nameof(events));
                _subscriptions = new[]
                {
                    events.Subscribe<HitLanded>(e => Fight(e.Attacker, e.Target)),
                    events.Subscribe<HitBlocked>(e => Fight(e.Attacker, e.Target)),
                    events.Subscribe<ActorKilled>(e => _modes.Fallen(e.Actor)),
                };
            }

            public void Dispose()
            {
                foreach (var subscription in _subscriptions)
                    subscription.Dispose();
            }

            // Both against each other; a blow at oneself (nothing does it, but a mod might) starts no fight.
            void Fight(ActorId attacker, ActorId target)
            {
                if (attacker.IsDefault || target.IsDefault || attacker == target)
                    return;
                _modes.Fight(attacker, target);
                _modes.Fight(target, attacker);
            }
        }
    }
}
