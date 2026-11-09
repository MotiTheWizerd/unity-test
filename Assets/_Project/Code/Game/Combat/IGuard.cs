using System.Numerics;
using Forge.Data;
using Forge.Game.Actors;

namespace Forge.Game.Combat
{
    /// <summary>
    /// An actor's guard: what a blow coming at it meets first (a raised shield), as its own fighting says
    /// (<see cref="ActorCombat"/>). Who strikes asks it before the blow lands (<see cref="Combatants"/> keeps it beside the
    /// actor's attributes), so the blow is stopped however it came: a blade, an arrow.
    /// </summary>
    public interface IGuard
    {
        /// <summary>
        /// <paramref name="attacker"/>'s blow with <paramref name="weapon"/>, of <paramref name="damage"/> health,
        /// travelling <paramref name="direction"/> (scene space; null: from wherever), comes at the actor: how much of it
        /// the guard stops, 0 to 1. 0 when no guard is up, the blow comes from where it doesn't cover, or holding it costs
        /// more stamina than is left, which breaks it.
        /// </summary>
        float Block(ActorId attacker, RecordId weapon, float damage, Vector3? direction);
    }
}
