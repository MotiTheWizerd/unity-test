using System;

namespace Forge.Game.Player
{
    /// <summary>
    /// How the player moves: speeds in meters per second, accelerations in meters per second squared. Immutable; the
    /// Unity side builds one from its player profile, tests build their own.
    /// </summary>
    public sealed class LocomotionSettings
    {
        public static readonly LocomotionSettings Default = new LocomotionSettings();

        /// <param name="runSpeed">The everyday pace, as in Skyrim, where the default is a run.</param>
        /// <param name="sprintSpeed">While sprint is held.</param>
        /// <param name="jumpHeight">How high a jump lifts the feet, in meters.</param>
        /// <param name="gravity">Pull down while in the air; stronger than Earth's feels right in games.</param>
        /// <param name="groundAcceleration">How fast the pace changes on the ground (start, stop, turn).</param>
        /// <param name="airAcceleration">How much a jump or fall can still be steered.</param>
        /// <param name="maxFallSpeed">Falling never gets faster than this.</param>
        /// <param name="coyoteTime">Seconds after stepping off an edge in which a jump still counts, so it never feels missed.</param>
        /// <param name="turnRate">Degrees per second the body turns toward where it goes, when it faces its movement.</param>
        /// <param name="walkSpeed">The pace while walking is switched on.</param>
        /// <param name="crouchSpeed">The pace while crouched.</param>
        public LocomotionSettings(
            float runSpeed = 4.5f,
            float sprintSpeed = 7.5f,
            float jumpHeight = 1.1f,
            float gravity = 18f,
            float groundAcceleration = 45f,
            float airAcceleration = 6f,
            float maxFallSpeed = 50f,
            float coyoteTime = 0.12f,
            float turnRate = 720f,
            float walkSpeed = 1.8f,
            float crouchSpeed = 1.2f)
        {
            TurnRate = Positive(turnRate, nameof(turnRate));
            WalkSpeed = Positive(walkSpeed, nameof(walkSpeed));
            CrouchSpeed = Positive(crouchSpeed, nameof(crouchSpeed));
            RunSpeed = Positive(runSpeed, nameof(runSpeed));
            SprintSpeed = Positive(sprintSpeed, nameof(sprintSpeed));
            JumpHeight = NotNegative(jumpHeight, nameof(jumpHeight));
            Gravity = Positive(gravity, nameof(gravity));
            GroundAcceleration = Positive(groundAcceleration, nameof(groundAcceleration));
            AirAcceleration = NotNegative(airAcceleration, nameof(airAcceleration));
            MaxFallSpeed = Positive(maxFallSpeed, nameof(maxFallSpeed));
            CoyoteTime = NotNegative(coyoteTime, nameof(coyoteTime));
        }

        public float WalkSpeed { get; }

        public float CrouchSpeed { get; }

        public float RunSpeed { get; }

        public float SprintSpeed { get; }

        public float JumpHeight { get; }

        public float Gravity { get; }

        public float GroundAcceleration { get; }

        public float AirAcceleration { get; }

        public float MaxFallSpeed { get; }

        public float CoyoteTime { get; }

        public float TurnRate { get; }

        /// <summary>The upward speed that reaches <see cref="JumpHeight"/> under <see cref="Gravity"/>.</summary>
        public float JumpSpeed => (float)Math.Sqrt(2.0 * Gravity * JumpHeight);

        static float Positive(float value, string name) =>
            value > 0f && !float.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name, value, "Must be a positive number.");

        static float NotNegative(float value, string name) =>
            value >= 0f && !float.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name, value, "Must be zero or more.");
    }
}
