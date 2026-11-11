using System;
using System.Numerics;
using Forge.Core.Geometry;

namespace Forge.Game.Motion
{
    /// <summary>
    /// How the body actually moved in a frame, as its look (animations) shows it: which way and how fast over the ground
    /// (<see cref="Ground"/>; as the body has it, <see cref="Relative"/>: a sidestep, a step back), how fast up or down,
    /// and whether it stands. Measured from where the body went, not where it was asked to go, so running into a wall
    /// shows standing, not running on the spot. Whether it is crouched, too, which its moves are then.
    /// </summary>
    public readonly struct BodyMotion
    {
        /// <param name="ground">Meters per second over the ground, world axes (x east, y north).</param>
        /// <param name="verticalSpeed">Meters per second up (positive) or down (negative).</param>
        public BodyMotion(Vector2 ground, float verticalSpeed, bool grounded, bool crouched = false)
        {
            Ground = ground;
            GroundSpeed = ground.Length();
            VerticalSpeed = verticalSpeed;
            Grounded = grounded;
            Crouched = crouched;
        }

        /// <summary>A motion of which only the speed over the ground is known (a sound's, a test's): taken as going north.</summary>
        public BodyMotion(float groundSpeed, float verticalSpeed, bool grounded, bool crouched = false)
            : this(new Vector2(0f, groundSpeed), verticalSpeed, grounded, crouched)
        {
        }

        /// <summary>Standing still on the ground.</summary>
        public static BodyMotion Still => new BodyMotion(Vector2.Zero, 0f, true);

        /// <summary>Meters per second over the ground, world axes (x east, y north); zero standing still.</summary>
        public Vector2 Ground { get; }

        /// <summary>Meters per second over the ground (never negative).</summary>
        public float GroundSpeed { get; }

        /// <summary>Meters per second up (positive) or down (negative).</summary>
        public float VerticalSpeed { get; }

        /// <summary>Standing on something at the end of the frame.</summary>
        public bool Grounded { get; }

        /// <summary>Crouched at the end of the frame.</summary>
        public bool Crouched { get; }

        /// <summary>In the air and going up: the start of a jump.</summary>
        public bool Rising => !Grounded && VerticalSpeed > 0f;

        /// <summary>In the air and coming down.</summary>
        public bool Falling => !Grounded && VerticalSpeed <= 0f;

        /// <summary>
        /// Its travel over the ground as the body facing <paramref name="heading"/> (compass degrees) has it: x to the
        /// body's right, y ahead, meters per second; so a body facing its foe sidesteps or backs away.
        /// </summary>
        public Vector2 Relative(float heading) =>
            new Vector2(Vector2.Dot(Ground, Compass.Right(heading)), Vector2.Dot(Ground, Compass.Forward(heading)));

        /// <summary>The motion of a body that went from <paramref name="from"/> to <paramref name="to"/> in <paramref name="deltaTime"/> seconds.</summary>
        public static BodyMotion Between(Vector3 from, Vector3 to, float deltaTime, bool grounded, bool crouched = false)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Must be a positive number.");
            var moved = to - from;
            return new BodyMotion(new Vector2(moved.X, moved.Z) / deltaTime, moved.Y / deltaTime, grounded, crouched);
        }
    }
}
