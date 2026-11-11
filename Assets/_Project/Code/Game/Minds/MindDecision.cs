using System.Numerics;

namespace Forge.Game.Minds
{
    /// <summary>
    /// What a mind decides for the next frame: how its body moves over the ground (<see cref="Velocity"/>, meters a
    /// second in the space it sees in; zero to stand), which way it faces (<see cref="Facing"/>, a compass heading;
    /// null: the way it goes), and what its hands do, as the body's commands have them (<see cref="Forge.Game.Actors.ActorCommands"/>):
    /// the ready-weapon key, the right hand used (a blow), the left hand used (the shield raised) or let go of (lowered).
    /// </summary>
    public readonly struct MindDecision
    {
        /// <summary>Standing still, facing the way it was, the hands idle.</summary>
        public static readonly MindDecision Stand = default;

        public MindDecision(Vector2 velocity, float? facing = null, bool readyWeapon = false, bool useRight = false, bool useLeft = false,
            bool releaseLeft = false)
        {
            Velocity = velocity;
            Facing = facing;
            ReadyWeapon = readyWeapon;
            UseRight = useRight;
            UseLeft = useLeft;
            ReleaseLeft = releaseLeft;
        }

        /// <summary>Walking at <paramref name="velocity"/>, facing the way it goes, the hands idle.</summary>
        public static MindDecision Walk(Vector2 velocity) => new MindDecision(velocity);

        /// <summary>How the body moves over the ground, meters a second; zero to stand.</summary>
        public Vector2 Velocity { get; }

        /// <summary>The compass heading the body faces, moving or standing; null: the way it goes.</summary>
        public float? Facing { get; }

        /// <summary>Draws the weapons, or puts them away.</summary>
        public bool ReadyWeapon { get; }

        /// <summary>Uses what the right hand holds: a blow.</summary>
        public bool UseRight { get; }

        /// <summary>Uses what the left hand holds: raises the shield, held until let go of.</summary>
        public bool UseLeft { get; }

        /// <summary>Lets go of what the left hand held: lowers the shield.</summary>
        public bool ReleaseLeft { get; }

        /// <summary>Whether it does anything with its hands or weapons this frame.</summary>
        public bool HandsBusy => ReadyWeapon || UseRight || UseLeft || ReleaseLeft;
    }
}
