using System.Numerics;
using Forge.Game.Actors;
using Forge.Game.Equipment;

namespace Forge.Game.Minds
{
    /// <summary>
    /// What a character's mind sees of its situation each frame, on the ground plane (x, z) in the space its home is
    /// given in: where it stands, what it is up to (<see cref="Mode"/>), where its nearest opponent stands, whether its
    /// weapons are out, what it fights with (<see cref="Shoots"/>), and what its hands are doing. Plain values, so a mind
    /// is tested without a body.
    /// </summary>
    public readonly struct MindSight
    {
        /// <param name="opponent">Where its nearest opponent stands, while it fights one that stands in the game; null otherwise.</param>
        /// <param name="weapons">Whether its weapons are out, coming out, away or going back.</param>
        /// <param name="rightHandBusy">Whether its right hand is swinging now (until the swing is over), a bow's draw and loose too.</param>
        /// <param name="guardUp">Whether its guard is up (the shield raised, or coming up).</param>
        /// <param name="shoots">Whether what its right hand fights with shoots (a bow, held in both hands).</param>
        /// <param name="drawPull">How far its bow is drawn, 0 (not drawn, or the arrow not yet on the string) to 1 (full draw).</param>
        public MindSight(Vector2 position, Mode mode, Vector2? opponent, Readiness weapons, bool rightHandBusy, bool guardUp,
            bool shoots = false, float drawPull = 0f)
        {
            Position = position;
            Mode = mode;
            Opponent = opponent;
            Weapons = weapons;
            RightHandBusy = rightHandBusy;
            GuardUp = guardUp;
            Shoots = shoots;
            DrawPull = drawPull;
        }

        /// <summary>At ease at <paramref name="position"/>: no one about, weapons away, hands idle.</summary>
        public static MindSight AtEase(Vector2 position) => new MindSight(position, Mode.Normal, null, Readiness.Sheathed, false, false);

        /// <summary>Where it stands.</summary>
        public Vector2 Position { get; }

        /// <summary>What it is up to: at ease, or in a fight.</summary>
        public Mode Mode { get; }

        /// <summary>Where its nearest opponent stands, while it fights one that stands in the game; null otherwise.</summary>
        public Vector2? Opponent { get; }

        /// <summary>Whether its weapons are out, coming out, away or going back.</summary>
        public Readiness Weapons { get; }

        /// <summary>Whether its right hand is swinging now.</summary>
        public bool RightHandBusy { get; }

        /// <summary>Whether its guard is up (the shield raised, or coming up).</summary>
        public bool GuardUp { get; }

        /// <summary>Whether what its right hand fights with shoots (a bow): it fights from afar.</summary>
        public bool Shoots { get; }

        /// <summary>How far its bow is drawn, 0 (not drawn, or the arrow not yet on the string) to 1 (full draw).</summary>
        public float DrawPull { get; }
    }
}
