using System;

namespace Forge.Game.Cameras
{
    /// <summary>How the third-person camera follows the player: distances in meters. Immutable.</summary>
    public sealed class ThirdPersonSettings
    {
        public static readonly ThirdPersonSettings Default = new ThirdPersonSettings();

        /// <param name="distance">How far behind it starts.</param>
        /// <param name="minDistance">The closest the wheel brings it; one notch nearer goes to first person.</param>
        /// <param name="maxDistance">The farthest the wheel takes it.</param>
        /// <param name="zoomStep">Meters per wheel notch.</param>
        /// <param name="pivotHeight">Height above the feet it looks from and orbits around (about the shoulders).</param>
        /// <param name="shoulderOffset">Meters to the right of the body it sits, so the player doesn't hide what's ahead.</param>
        /// <param name="radius">How thick the camera is to walls: it stops this far short of them.</param>
        /// <param name="returnSpeed">Meters per second it eases back out once a wall stops blocking it (it pulls in at once).</param>
        /// <param name="zoomEase">Seconds the camera takes to glide most of the way (two thirds) to where the wheel sent it; zero jumps there.</param>
        public ThirdPersonSettings(
            float distance = 3.5f,
            float minDistance = 1.2f,
            float maxDistance = 8f,
            float zoomStep = 0.6f,
            float pivotHeight = 1.6f,
            float shoulderOffset = 0.35f,
            float radius = 0.2f,
            float returnSpeed = 6f,
            float zoomEase = 0.12f)
        {
            if (!(zoomEase >= 0f) || float.IsInfinity(zoomEase))
                throw new ArgumentOutOfRangeException(nameof(zoomEase), zoomEase, "Must be a number of seconds, zero or more.");
            ZoomEase = zoomEase;
            MinDistance = Positive(minDistance, nameof(minDistance));
            MaxDistance = Positive(maxDistance, nameof(maxDistance));
            if (MaxDistance < MinDistance)
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "Must be at least the min distance.");
            Distance = Clamp(Positive(distance, nameof(distance)));
            ZoomStep = Positive(zoomStep, nameof(zoomStep));
            PivotHeight = Positive(pivotHeight, nameof(pivotHeight));
            ShoulderOffset = Finite(shoulderOffset, nameof(shoulderOffset));
            Radius = Positive(radius, nameof(radius));
            ReturnSpeed = Positive(returnSpeed, nameof(returnSpeed));
        }

        public float Distance { get; }

        public float MinDistance { get; }

        public float MaxDistance { get; }

        public float ZoomStep { get; }

        public float PivotHeight { get; }

        public float ShoulderOffset { get; }

        public float Radius { get; }

        public float ReturnSpeed { get; }

        /// <summary>Seconds the camera takes to glide two thirds of the way to where the wheel sent it; zero jumps.</summary>
        public float ZoomEase { get; }

        /// <summary><paramref name="distance"/> held within [min, max].</summary>
        public float Clamp(float distance) => Math.Max(MinDistance, Math.Min(MaxDistance, distance));

        static float Positive(float value, string name) =>
            value > 0f && !float.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name, value, "Must be a positive number.");

        static float Finite(float value, string name) =>
            !float.IsNaN(value) && !float.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name, value, "Must be a number.");
    }
}
