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
    /// <para>
    /// A <see cref="CombatMove.Held"/> move (a bow's draw) comes on only while it is <see cref="Holding"/>: it stops at
    /// the end of its hit window (full draw) and stays there until let go; let go, it lets its blow go as soon as its window
    /// is open (the arrow is on the string), and plays on from the window's end. A swing of a weapon that
    /// <see cref="Shoots"/> lands nothing itself: what it looses does (<see cref="Shot"/>).
    /// </para>
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
            Holding = move.Held;
        }

        /// <summary>The slot it was asked for with (the right hand).</summary>
        public RecordId Slot { get; }

        /// <summary>What swings, and the slots it takes.</summary>
        public EquippedItem Weapon { get; }

        public CombatMove Move { get; }

        public MoveTiming Timing { get; }

        /// <summary>Seconds since it began, less any it was held still.</summary>
        public float Elapsed { get; private set; }

        /// <summary>How far it has come, 0 to 1.</summary>
        public float Progress => Elapsed >= Timing.Seconds ? 1f : Elapsed / Timing.Seconds;

        public SwingPhase Phase =>
            Progress < Timing.HitFrom ? SwingPhase.WindUp : Progress <= Timing.HitTo ? SwingPhase.Striking : SwingPhase.Recovery;

        /// <summary>Whether its blow can land now.</summary>
        public bool IsStriking => Phase == SwingPhase.Striking;

        /// <summary>Whether a held move is still held (the attack not yet released); never for one that isn't held.</summary>
        public bool Holding { get; private set; }

        /// <summary>Whether it is held still at the end of its window (a bow at full draw).</summary>
        public bool IsHeldStill => Holding && Elapsed >= HoldAt;

        /// <summary>Whether its weapon shoots (a bow): its blow is what it looses.</summary>
        public bool Shoots => Weapon.Record.Weapon?.Ammo != null;

        /// <summary>Whether it has let its blow go (an arrow loosed).</summary>
        public bool Loosed { get; private set; }

        /// <summary>How far into its window it has come, 0 (just open: the arrow on the string) to 1 (full draw).</summary>
        public float Pull =>
            Timing.HitTo > Timing.HitFrom ? Clamp01((Progress - Timing.HitFrom) / (Timing.HitTo - Timing.HitFrom)) : 1f;

        // A held move held still is not over, even at the move's end (a clip that ends at full draw).
        public bool IsOver => !Holding && Elapsed >= Timing.Seconds;

        /// <summary>Whom it has struck.</summary>
        public IReadOnlyCollection<ActorId> Struck => _struck;

        float HoldAt => Timing.HitTo * Timing.Seconds;

        // Held, it stops at full draw; let go before its window opened, it stops where it opens, to be let go from there.
        internal void Advance(float seconds)
        {
            Elapsed += seconds;
            if (Holding && Elapsed > HoldAt)
                Elapsed = HoldAt;
            else if (Move.Held && !Holding && !Loosed && Elapsed > OpensAt)
                Elapsed = OpensAt;
        }

        float OpensAt => Timing.HitFrom * Timing.Seconds;

        // Whether its window has opened (counted in seconds, as it is stopped, so the stop is exactly at it).
        internal bool WindowOpen => Elapsed >= OpensAt;

        internal void Release() => Holding = false;

        // Let go: on from the end of its window (what was left of the draw is the shot).
        internal void Loose()
        {
            Loosed = true;
            if (Elapsed < HoldAt)
                Elapsed = HoldAt;
        }

        internal bool MarkStruck(ActorId target) => _struck.Add(target);

        static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
