using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Equipment;

namespace Forge.Game.Combat
{
    /// <summary>Where a swing is: winding up, in its hit window, recovering.</summary>
    public enum SwingPhase
    {
        WindUp,
        Striking,
        Recovery,
    }

    /// <summary>
    /// One combat move playing in one hand (or both, for a two-hander: the slots of its fit): what swings, which move,
    /// how it is timed, how far it has come, and whom it has struck, so a blow lands on each target once.
    /// </summary>
    public sealed class Swing
    {
        readonly HashSet<ActorId> _struck = new HashSet<ActorId>();

        internal Swing(RecordId slot, EquippedItem weapon, CombatMove move, MoveTiming timing)
        {
            Slot = slot;
            Weapon = weapon;
            Move = move;
            Timing = timing;
        }

        /// <summary>The slot it was asked for with (the right hand).</summary>
        public RecordId Slot { get; }

        /// <summary>What swings, and the slots it takes.</summary>
        public EquippedItem Weapon { get; }

        public CombatMove Move { get; }

        public MoveTiming Timing { get; }

        /// <summary>Seconds since it began.</summary>
        public float Elapsed { get; private set; }

        /// <summary>How far it has come, 0 to 1.</summary>
        public float Progress => Elapsed >= Timing.Seconds ? 1f : Elapsed / Timing.Seconds;

        public SwingPhase Phase =>
            Progress < Timing.HitFrom ? SwingPhase.WindUp : Progress <= Timing.HitTo ? SwingPhase.Striking : SwingPhase.Recovery;

        /// <summary>Whether its blow can land now.</summary>
        public bool IsStriking => Phase == SwingPhase.Striking;

        public bool IsOver => Elapsed >= Timing.Seconds;

        /// <summary>Whom it has struck.</summary>
        public IReadOnlyCollection<ActorId> Struck => _struck;

        internal void Advance(float seconds) => Elapsed += seconds;

        internal bool MarkStruck(ActorId target) => _struck.Add(target);
    }
}
