using System;
using System.Numerics;

namespace Forge.Game.Player
{
    /// <summary>
    /// How the body actually moved in a frame, as its look (animations) shows it: how fast over the ground, how fast up
    /// or down, and whether it stands. Measured from where the body went, not where it was asked to go, so running into
    /// a wall shows standing, not running on the spot.
    /// </summary>
    public readonly struct BodyMotion
    {
        public BodyMotion(float groundSpeed, float verticalSpeed, bool grounded)
        {
            GroundSpeed = groundSpeed;
            VerticalSpeed = verticalSpeed;
            Grounded = grounded;
        }

        /// <summary>Standing still on the ground.</summary>
        public static BodyMotion Still => new BodyMotion(0f, 0f, true);

        /// <summary>Meters per second over the ground (never negative).</summary>
        public float GroundSpeed { get; }

        /// <summary>Meters per second up (positive) or down (negative).</summary>
        public float VerticalSpeed { get; }

        /// <summary>Standing on something at the end of the frame.</summary>
        public bool Grounded { get; }

        /// <summary>In the air and going up: the start of a jump.</summary>
        public bool Rising => !Grounded && VerticalSpeed > 0f;

        /// <summary>In the air and coming down.</summary>
        public bool Falling => !Grounded && VerticalSpeed <= 0f;

        /// <summary>The motion of a body that went from <paramref name="from"/> to <paramref name="to"/> in <paramref name="deltaTime"/> seconds.</summary>
        public static BodyMotion Between(Vector3 from, Vector3 to, float deltaTime, bool grounded)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Must be a positive number.");
            var moved = to - from;
            return new BodyMotion(new Vector2(moved.X, moved.Z).Length() / deltaTime, moved.Y / deltaTime, grounded);
        }
    }
}
