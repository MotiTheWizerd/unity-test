using System;
using Forge.Game.Motion;

namespace Forge.Game.Player
{
    /// <summary>How a body goes on its feet: which its steps sound like, and (later) how far they carry.</summary>
    public enum Gait
    {
        /// <summary>Standing, or off the ground: no steps.</summary>
        Still,

        /// <summary>Crouched and moving: soft steps.</summary>
        Sneak,

        Walk,

        Run,

        Sprint,
    }

    /// <summary>
    /// The gait of a body moving as it does: still below <see cref="MovingSpeed"/> or in the air, sneaking while crouched,
    /// else by its pace, each one up from halfway between its own and the next one down's (walk, run, sprint), so a pace
    /// eased a little either side of its speed still reads as itself. Engine-free.
    /// </summary>
    public static class Gaits
    {
        /// <summary>Meters per second over the ground below which a body stands rather than steps.</summary>
        public const float MovingSpeed = 0.3f;

        public static Gait Of(BodyMotion motion, LocomotionSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (!motion.Grounded || motion.GroundSpeed < MovingSpeed)
                return Gait.Still;
            if (motion.Crouched)
                return Gait.Sneak;
            if (motion.GroundSpeed < (settings.WalkSpeed + settings.RunSpeed) * 0.5f)
                return Gait.Walk;
            return motion.GroundSpeed < (settings.RunSpeed + settings.SprintSpeed) * 0.5f ? Gait.Run : Gait.Sprint;
        }
    }
}
