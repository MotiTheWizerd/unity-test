using System;

namespace Forge.Game.Player
{
    /// <summary>
    /// Keeps the feet from sliding. A body's ground moves (idle, walk, run, ...) are clips each made at its own speed,
    /// for a body of the size the clips were made on. For a body at some speed and size, the blend picks the clips by
    /// <see cref="BlendSpeed"/> (the speed in the clips' own size); past the fastest clip, that clip plays faster
    /// (<see cref="Pace"/>), so a sprint with only a run clip still keeps its feet on the ground.
    /// </summary>
    public readonly struct StrideMatch
    {
        StrideMatch(float blendSpeed, float pace)
        {
            BlendSpeed = blendSpeed;
            Pace = pace;
        }

        /// <summary>The speed the clips are blended at, in the clips' own size; never past the fastest clip.</summary>
        public float BlendSpeed { get; }

        /// <summary>How much faster than made the clips play: 1 up to the fastest clip, more beyond it.</summary>
        public float Pace { get; }

        /// <param name="groundSpeed">The body's speed over the ground, meters per second.</param>
        /// <param name="bodyScale">The body's size against the size the clips were made on (1: the same).</param>
        /// <param name="fastestClip">The speed of the fastest ground clip, in the clips' own size.</param>
        public static StrideMatch For(float groundSpeed, float bodyScale, float fastestClip)
        {
            if (!(bodyScale > 0f) || float.IsInfinity(bodyScale))
                throw new ArgumentOutOfRangeException(nameof(bodyScale), bodyScale, "Must be a positive number.");
            var speed = Math.Max(0f, groundSpeed) / bodyScale;
            if (!(fastestClip > 0f) || speed <= fastestClip)
                return new StrideMatch(speed, 1f);
            return new StrideMatch(fastestClip, speed / fastestClip);
        }
    }
}
