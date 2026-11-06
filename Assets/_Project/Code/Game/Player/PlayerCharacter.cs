using System;
using System.Numerics;

namespace Forge.Game.Player
{
    /// <summary>
    /// The player in the world: where it looks (<see cref="PlayerLook"/>), how it moves (<see cref="PlayerLocomotion"/>)
    /// and its body (<see cref="IPlayerMotor"/>). Each frame it turns the look, works out the move and has the body make
    /// it, keeping what the body touched for the next frame. Engine-free; positions are the motor's.
    /// </summary>
    public sealed class PlayerCharacter
    {
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
        }

        /// <summary>Stands the player at <paramref name="feet"/>, facing <paramref name="heading"/> and looking level, at rest.</summary>
        public void PlaceAt(Vector3 feet, float heading)
        {
            Motor.Teleport(feet);
            Look.Set(heading, 0f);
            Locomotion.Stop();
            _contact = default;
        }
    }
}
