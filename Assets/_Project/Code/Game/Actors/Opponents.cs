using System;
using System.Numerics;

namespace Forge.Game.Actors
{
    /// <summary>
    /// Which of an actor's opponents (<see cref="ActorModes.OpponentsOf"/>) it deals with: the nearest that stands. A
    /// character's mind closes in on it, the player's lock-on squares up to it (<see cref="Forge.Game.Player.SoftLockOn"/>).
    /// Engine-free; positions are in whatever space the actors' feet are given (the scene's).
    /// </summary>
    public static class Opponents
    {
        /// <summary>
        /// The nearest of <paramref name="actor"/>'s opponents to <paramref name="from"/> that stands in
        /// <paramref name="standing"/>, and where its feet are; false when it fights no one standing (at ease, cooling
        /// off, its foes fallen or gone).
        /// </summary>
        public static bool TryNearest(ActorModes modes, ActorId actor, Vector3 from, IStandingActors standing, out ActorId foe,
            out Vector3 feet)
        {
            if (modes == null)
                throw new ArgumentNullException(nameof(modes));
            if (standing == null)
                throw new ArgumentNullException(nameof(standing));

            foe = default;
            feet = default;
            var nearest = float.MaxValue;
            foreach (var opponent in modes.OpponentsOf(actor))
            {
                if (!(standing.FeetOf(opponent) is Vector3 at))
                    continue;
                var apart = Vector3.DistanceSquared(at, from);
                if (apart >= nearest)
                    continue;
                nearest = apart;
                foe = opponent;
                feet = at;
            }

            return !foe.IsDefault;
        }
    }
}
