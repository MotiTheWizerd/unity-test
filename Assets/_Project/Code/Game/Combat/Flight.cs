using System;
using System.Numerics;

namespace Forge.Game.Combat
{
    /// <summary>
    /// Where something thrown or shot is and how it moves (an arrow): gravity bends its path, nothing else slows it.
    /// <see cref="Step"/> moves it on, so whoever flies it through a world tests the way from where it was to where it is.
    /// Scene coordinates, meters and seconds, +Y up. Engine-free.
    /// </summary>
    public readonly struct Flight
    {
        /// <summary>How fast it falls faster, meters a second each second.</summary>
        public const float Gravity = 9.81f;

        public Flight(Vector3 position, Vector3 velocity)
        {
            Position = position;
            Velocity = velocity;
        }

        public Vector3 Position { get; }

        /// <summary>Meters a second, and which way.</summary>
        public Vector3 Velocity { get; }

        /// <summary>Which way it points, the way it moves (an arrow flies point first); forward when still.</summary>
        public Vector3 Heading => Velocity.LengthSquared() > 1e-8f ? Vector3.Normalize(Velocity) : Vector3.UnitZ;

        /// <summary>From <paramref name="from"/> towards <paramref name="towards"/> at <paramref name="speed"/> meters a second.</summary>
        public static Flight Toward(Vector3 from, Vector3 towards, float speed)
        {
            if (!(speed > 0f) || float.IsInfinity(speed))
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "A flight's speed is a finite number above zero.");
            var way = towards - from;
            var heading = way.LengthSquared() > 1e-8f ? Vector3.Normalize(way) : Vector3.UnitZ;
            return new Flight(from, heading * speed);
        }

        /// <summary>
        /// <paramref name="seconds"/> later: falling as it goes. The velocity is taken at the step's end (semi-implicit
        /// Euler), so a long flight's height stays true to within a step's fall.
        /// </summary>
        public Flight Step(float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
                return this;
            var velocity = Velocity - Vector3.UnitY * (Gravity * seconds);
            return new Flight(Position + velocity * seconds, velocity);
        }
    }
}
