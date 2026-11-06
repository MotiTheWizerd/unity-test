using System;
using System.Numerics;
using Forge.Core.Geometry;

namespace Forge.Game.Player
{
    /// <summary>
    /// The player in the world: where it looks (<see cref="PlayerLook"/>), how it moves (<see cref="PlayerLocomotion"/>),
    /// its body (<see cref="IPlayerMotor"/>) and which way that body faces (<see cref="BodyHeading"/>, by
    /// <see cref="Facing"/>). Each frame it turns the look, works out the move, has the body make it, keeping what the
    /// body touched for the next frame, and turns the body. Engine-free; positions are the motor's.
    /// </summary>
    public sealed class PlayerCharacter
    {
        // Slower than this on the ground (m/s), the body keeps the way it faces rather than following jitter.
        const float TurnSpeedThreshold = 0.2f;

        MotorContact _contact;

        /// <param name="eyeHeight">Meters from the feet to the eyes, where a first-person camera sits.</param>
        public PlayerCharacter(IPlayerMotor motor, LocomotionSettings locomotion, float eyeHeight)
        {
            Motor = motor ?? throw new ArgumentNullException(nameof(motor));
            if (!(eyeHeight > 0f) || float.IsInfinity(eyeHeight))
                throw new ArgumentOutOfRangeException(nameof(eyeHeight), eyeHeight, "Must be a positive number.");
            EyeHeight = eyeHeight;
            Locomotion = new PlayerLocomotion(locomotion);
        }

        public IPlayerMotor Motor { get; }

        public PlayerLook Look { get; } = new PlayerLook();

        public PlayerLocomotion Locomotion { get; }

        public float EyeHeight { get; }

        /// <summary>How the body turns; the camera mode sets it (first person: with the look).</summary>
        public BodyFacing Facing { get; set; } = BodyFacing.Look;

        /// <summary>Compass degrees the body faces, which its look (model, animations) is turned to.</summary>
        public float BodyHeading { get; private set; }

        /// <summary>Where the feet are.</summary>
        public Vector3 Feet => Motor.Position;

        /// <summary>Where the eyes are.</summary>
        public Vector3 Eyes => Motor.Position + new Vector3(0f, EyeHeight, 0f);

        /// <summary>Whether the last move ended standing on something.</summary>
        public bool IsGrounded => _contact.Grounded;

        /// <summary>Plays one frame of <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(PlayerCommands commands, float deltaTime)
        {
            Look.Turn(commands.Turn);
            var move = Locomotion.Step(commands, Look.Heading, _contact, deltaTime);
            if (move != Vector3.Zero)
                _contact = Motor.Move(move);
            TurnBody(deltaTime);
        }

        /// <summary>Stands the player at <paramref name="feet"/>, body and look facing <paramref name="heading"/>, level, at rest.</summary>
        public void PlaceAt(Vector3 feet, float heading)
        {
            Motor.Teleport(feet);
            Look.Set(heading, 0f);
            BodyHeading = Look.Heading;
            Locomotion.Stop();
            _contact = default;
        }

        void TurnBody(float deltaTime)
        {
            if (Facing == BodyFacing.Look)
            {
                BodyHeading = Look.Heading;
                return;
            }

            var velocity = Locomotion.Velocity;
            var ground = new Vector2(velocity.X, velocity.Z);
            if (ground.Length() < TurnSpeedThreshold || !(deltaTime > 0f))
                return;
            BodyHeading = TurnToward(BodyHeading, Compass.HeadingOf(ground), Locomotion.Settings.TurnRate * deltaTime);
        }

        // The heading turned toward target the short way round, by at most step degrees.
        static float TurnToward(float heading, float target, float step)
        {
            var difference = Compass.Normalize(target - heading + 180f) - 180f;
            return Compass.Normalize(heading + Math.Max(-step, Math.Min(step, difference)));
        }
    }
}
