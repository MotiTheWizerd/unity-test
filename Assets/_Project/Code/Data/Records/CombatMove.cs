using System;

namespace Forge.Data.Records
{
    /// <summary>What a combat move is for, which is how an actor asks for it ("attack with the right hand").</summary>
    public enum CombatMoveKind
    {
        /// <summary>A plain blow (a click).</summary>
        Attack,
    }

    /// <summary>
    /// One move of a <see cref="CombatStyleRecord"/>: what it is for, the animation that plays it (a move of the actor's
    /// move set, "SwordSlash"), and the stamina it costs. How long it lasts and when in it a blow can land are the
    /// animation's to say, measured from the clip, so no move is timed by hand.
    /// </summary>
    public sealed class CombatMove
    {
        /// <param name="animation">The move set's move that plays it ("SwordSlash"; a clip named so).</param>
        /// <param name="staminaCost">Stamina it takes to start; zero or more.</param>
        public CombatMove(CombatMoveKind kind, string animation, float staminaCost = 0f)
        {
            if (!Enum.IsDefined(typeof(CombatMoveKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown combat move kind.");
            if (string.IsNullOrWhiteSpace(animation))
                throw new ArgumentException("A combat move names the animation that plays it.", nameof(animation));
            if (!(staminaCost >= 0f) || float.IsInfinity(staminaCost))
                throw new ArgumentOutOfRangeException(nameof(staminaCost), staminaCost, "A stamina cost must be a finite number, zero or more.");

            Kind = kind;
            Animation = animation;
            StaminaCost = staminaCost;
        }

        public CombatMoveKind Kind { get; }

        public string Animation { get; }

        public float StaminaCost { get; }
    }
}
