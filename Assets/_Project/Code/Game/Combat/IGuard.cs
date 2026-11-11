using System.Numerics;
using Forge.Data;
using Forge.Game.Actors;

namespace Forge.Game.Combat
{
    /// <summary>
    /// How an actor that fights meets a blow, as its own fighting says (<see cref="ActorCombat"/>): its guard (a raised
    /// shield) stops what it can, and the blow's impact jolts it. Who strikes asks it before the blow lands
    /// (<see cref="Combatants"/> keeps it beside the actor's attributes), so the blow is met however it came: a blade, an
    /// arrow.
    /// </summary>
    public interface IGuard
    {
        /// <summary>
        /// <paramref name="attacker"/>'s blow with <paramref name="weapon"/>, of <paramref name="damage"/> health,
        /// travelling <paramref name="direction"/> (scene space; null: from wherever), meeting it where
        /// <paramref name="met"/> says, comes at the actor: how much of it the guard stops, 0 to 1. 0 when no guard is up,
        /// the blow met the body past it, or came from where it doesn't cover, or holding it costs more stamina than is
        /// left, which breaks it.
        /// </summary>
        float Block(ActorId attacker, RecordId weapon, float damage, Vector3? direction, BlowMet met = BlowMet.Anywhere);

        /// <summary>A blow struck it, on its guard or its body: whatever it is doing stops for <paramref name="seconds"/> (the impact, felt by both).</summary>
        void Jolt(float seconds);
    }
}
