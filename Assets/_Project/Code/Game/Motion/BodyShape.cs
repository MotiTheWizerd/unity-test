using System;

namespace Forge.Game.Motion
{
    /// <summary>
    /// The size of an actor's body, a standing capsule: how tall, how wide, how wide with its weapons in hand (its guard
    /// held before it), how tall crouched, the highest step it walks up and the steepest slope. The player's comes from
    /// its profile, a character's from the same with its own height (<see cref="WithHeight"/>). Kept sound: never wider
    /// than tall, crouched between its width and its height, its guard no narrower than itself and fitting its crouch, a
    /// step no higher than the room between its round ends. Meters and degrees.
    /// </summary>
    public readonly struct BodyShape
    {
        /// <summary>From the eyes to the top of the head, meters: how much taller a body is than its eyes are high.</summary>
        public const float HeadAboveEyes = 0.12f;

        /// <param name="guardRadius">How far from its middle it keeps others while its weapons are in hand (a shield held
        /// before it): two guards meet rather than sink into each other; 0 (or less than <paramref name="radius"/>): as wide
        /// as ever.</param>
        public BodyShape(float height, float radius, float crouchHeight, float stepHeight, float slopeLimit, float guardRadius = 0f)
        {
            if (!(height > 0f) || float.IsInfinity(height))
                throw new ArgumentOutOfRangeException(nameof(height), height, "Must be a positive number.");
            if (!(radius > 0f) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Must be a positive number.");
            if (float.IsNaN(guardRadius) || float.IsInfinity(guardRadius))
                throw new ArgumentOutOfRangeException(nameof(guardRadius), guardRadius, "Must be a number.");
            Height = height;
            Radius = Math.Min(radius, height * 0.5f);
            CrouchHeight = Math.Max(2f * Radius, Math.Min(crouchHeight, height));
            GuardRadius = Math.Max(Radius, Math.Min(guardRadius, CrouchHeight * 0.5f));
            StepHeight = Math.Max(0f, Math.Min(stepHeight, height - 2f * Radius));
            SlopeLimit = Math.Max(0f, Math.Min(slopeLimit, 89f));
        }

        public float Height { get; }

        public float Radius { get; }

        /// <summary>How wide it is with its weapons in hand, from its middle: its guard, which others keep clear of.</summary>
        public float GuardRadius { get; }

        /// <summary>How tall it is crouched: what it passes under.</summary>
        public float CrouchHeight { get; }

        /// <summary>The highest step walked up without jumping.</summary>
        public float StepHeight { get; }

        /// <summary>The steepest ground walked up, degrees.</summary>
        public float SlopeLimit { get; }

        /// <summary>
        /// The same body <paramref name="height"/> tall: as wide, guarding as wide, stepping as high and climbing as steep,
        /// crouching to the same share of its height.
        /// </summary>
        public BodyShape WithHeight(float height) =>
            new BodyShape(height, Radius, CrouchHeight / Height * height, StepHeight, SlopeLimit, GuardRadius);

        /// <summary>The same body as tall as one whose eyes are <paramref name="eyeHeight"/> above its feet.</summary>
        public BodyShape ForEyes(float eyeHeight) => WithHeight(eyeHeight + HeadAboveEyes);
    }
}
