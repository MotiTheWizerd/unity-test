using System;

namespace Forge.Data.Records
{
    /// <summary>What a combat move is for, which is how an actor asks for it ("attack with the right hand").</summary>
    public enum CombatMoveKind
    {
        /// <summary>A plain blow (a click).</summary>
        Attack,

        /// <summary>Taking the weapon from where it is carried into the hand (the ready-weapon key).</summary>
        Draw,

        /// <summary>Putting the weapon back where it is carried.</summary>
        Sheathe,

        /// <summary>
        /// How it stands with the weapon drawn (a bow held low, ready): its drawn idle, which its drawn ground moves start
        /// from; a style without one stands as the actor's move set does with any weapon drawn.
        /// </summary>
        Stance,

        /// <summary>Raising the weapon against blows (a shield), held for as long as asked; its weapon's block part stops them.</summary>
        Block,

        /// <summary>What plays when a block takes a blow (the shield jolted).</summary>
        Blocked,

        /// <summary>What plays when the actor dies with this style's weapon in hand, and stays: it falls, and lies as it fell.</summary>
        Death,
    }

    /// <summary>
    /// One move of a <see cref="CombatStyleRecord"/>: what it is for, the animation that plays it (a move of the actor's
    /// move set, "SwordSlash"), the stamina it costs, whether it is <see cref="Held"/> (a bow's draw) and, if so, what
    /// plays once it is let go (<see cref="Release"/>). How long it lasts and when in it a blow can land are the
    /// animation's to say, measured from the clip, so no move is timed by hand.
    /// </summary>
    public sealed class CombatMove
    {
        /// <param name="animation">The move set's move that plays it ("SwordSlash"; a clip named so).</param>
        /// <param name="staminaCost">Stamina it takes to start; zero or more.</param>
        /// <param name="held">Whether it is drawn out for as long as the attack is held, and let go on release (a bow's draw).</param>
        /// <param name="release">The move set's move that plays once a held move is let go ("BowRelease"); null: the held one plays on.</param>
        public CombatMove(CombatMoveKind kind, string animation, float staminaCost = 0f, bool held = false, string release = null)
        {
            if (!Enum.IsDefined(typeof(CombatMoveKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown combat move kind.");
            if (string.IsNullOrWhiteSpace(animation))
                throw new ArgumentException("A combat move names the animation that plays it.", nameof(animation));
            if (!(staminaCost >= 0f) || float.IsInfinity(staminaCost))
                throw new ArgumentOutOfRangeException(nameof(staminaCost), staminaCost, "A stamina cost must be a finite number, zero or more.");
            if (release != null && (string.IsNullOrWhiteSpace(release) || !held))
                throw new ArgumentException("A release names an animation, and only a held move is let go.", nameof(release));

            Kind = kind;
            Animation = animation;
            StaminaCost = staminaCost;
            Held = held;
            Release = release;
        }

        public CombatMoveKind Kind { get; }

        public string Animation { get; }

        public float StaminaCost { get; }

        /// <summary>
        /// Whether it is drawn out while the attack is held: it plays on up to the end of its hit window and stays there,
        /// and lets its blow go when the attack is released (a bow's draw: release looses the arrow, Skyrim's way).
        /// </summary>
        public bool Held { get; }

        /// <summary>
        /// What plays once a held move is let go, from where it lets its blow go (a bow's release: the string hand
        /// flicking back, the bow coming down); the move lasts until it is played out. Null: the held move plays on.
        /// </summary>
        public string Release { get; }
    }
}
