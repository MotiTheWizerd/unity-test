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
    /// is open (the arrow is on the string), and plays on from the window's end, or, when its move has a release, lasts
    /// as long as that plays (<see cref="ReleaseSeconds"/>). A swing of a weapon that <see cref="Shoots"/> lands nothing
    /// itself: what it looses does (<see cref="Shot"/>).
    /// </para>
    /// </summary>
    public sealed class Swing
    {
        readonly HashSet<ActorId> _struck = new HashSet<ActorId>();
        float _loosedAt;

        internal Swing(RecordId slot, EquippedItem weapon, CombatMove move, MoveTiming timing, float? releaseSeconds = null)
        {
            Slot = slot;
            Weapon = weapon;
            Move = move;
            Timing = timing;
            Holding = move.Held;
            ReleaseSeconds = releaseSeconds;
        }

        /// <summary>The slot it was asked for with (the right hand).</summary>
        public RecordId Slot { get; }

        /// <summary>What swings, and the slots it takes.</summary>
        public EquippedItem Weapon { get; }

        public CombatMove Move { get; }

        public MoveTiming Timing { get; }

        /// <summary>
        /// How long its release plays once let go (<see cref="CombatMove.Release"/>, as the look times it), seconds;
        /// null: let go, it plays on to the end of its move.
        /// </summary>
        public float? ReleaseSeconds { get; }

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

        /// <summary>Whether it is a block (a shield raised): it strikes no one, and in its window (raised) its guard is up.</summary>
        public bool Guards => Move.Kind == CombatMoveKind.Block;

        /// <summary>How its move's window is read off its clip: a raise, a draw or a cut.</summary>
        public MoveMeasure Measure => MeasureOf(Move, Weapon.Record.Weapon);

        /// <summary>How the window of <paramref name="move"/> with <paramref name="weapon"/> is read off its clip.</summary>
        public static MoveMeasure MeasureOf(CombatMove move, WeaponPart weapon) =>
            move.Kind == CombatMoveKind.Block ? MoveMeasure.Raise : weapon?.Ammo != null ? MoveMeasure.Draw : MoveMeasure.Cut;

        /// <summary>Whether it has let its blow go (an arrow loosed).</summary>
        public bool Loosed { get; private set; }

        /// <summary>How far into its window it has come, 0 (just open: the arrow on the string) to 1 (full draw).</summary>
        public float Pull =>
            Timing.HitTo > Timing.HitFrom ? Clamp01((Progress - Timing.HitFrom) / (Timing.HitTo - Timing.HitFrom)) : 1f;

        // A held move held still is not over, even at the move's end (a clip that ends at full draw); let go with a
        // release, it is over once that has played.
        public bool IsOver => !Holding && (Loosed && ReleaseSeconds.HasValue ? Elapsed - _loosedAt >= ReleaseSeconds.Value : Elapsed >= Timing.Seconds);

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

        // Let go: on from the end of its window (what was left of the draw is the shot), its release counted from here.
        internal void Loose()
        {
            Loosed = true;
            if (Elapsed < HoldAt)
                Elapsed = HoldAt;
            _loosedAt = Elapsed;
        }

        internal bool MarkStruck(ActorId target) => _struck.Add(target);

        static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
