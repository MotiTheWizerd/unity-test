using System;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Attributes;
using Forge.Game.Events;
using Forge.Game.Motion;

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
        /// <param name="attributes">The player's attributes, which moving costs (<see cref="MovementCosts"/>) are paid from
        /// and which tick with it; null for a player whose moves cost nothing.</param>
        /// <param name="events">The game's stream, told what happens to the player (exhausted, fell); null to tell no one.</param>
        public PlayerCharacter(IPlayerMotor motor, LocomotionSettings locomotion, float eyeHeight, ActorAttributes attributes = null, GameEvents events = null)
        {
            Motor = motor ?? throw new ArgumentNullException(nameof(motor));
            if (!(eyeHeight > 0f) || float.IsInfinity(eyeHeight))
                throw new ArgumentOutOfRangeException(nameof(eyeHeight), eyeHeight, "Must be a positive number.");
            EyeHeight = eyeHeight;
            Locomotion = new PlayerLocomotion(locomotion);
            if (attributes != null)
                Costs = new MovementCosts(attributes, events);
        }

        /// <summary>What moving costs the player, paid from its attributes; null when its moves cost nothing.</summary>
        public MovementCosts Costs { get; }

        /// <summary>The player's attributes; null when it has none.</summary>
        public ActorAttributes Attributes => Costs?.Attributes;

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

        /// <summary>How the body moved in the last frame, which its look (animations) shows.</summary>
        public BodyMotion Motion { get; private set; } = BodyMotion.Still;

        /// <summary>Plays one frame of <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(PlayerCommands commands, float deltaTime)
        {
            Look.Turn(commands.Turn);
            if (Costs != null)
                commands = Costs.Afford(commands, deltaTime);
            var from = Motor.Position;
            var move = Locomotion.Step(commands, Look.Heading, _contact, deltaTime);
            if (move != Vector3.Zero)
                _contact = Motor.Move(move);
            if (deltaTime > 0f)
            {
                Motion = BodyMotion.Between(from, Motor.Position, deltaTime, _contact.Grounded);
                Costs?.Pay(Locomotion.Jumped, _contact.Grounded, Motor.Position.Y);
                Attributes?.Tick(deltaTime);
            }

            TurnBody(deltaTime);
        }

        /// <summary>The world moved under the player by <paramref name="delta"/> (a rebase); the player moves with it, mid-fall or not.</summary>
        public void Shift(Vector3 delta)
        {
            Motor.Teleport(Motor.Position + delta);
            Costs?.Shift(delta.Y);
        }

        /// <summary>Stands the player at <paramref name="feet"/>, body and look facing <paramref name="heading"/>, level, at rest.</summary>
        public void PlaceAt(Vector3 feet, float heading)
        {
            Motor.Teleport(feet);
            Look.Set(heading, 0f);
            BodyHeading = Look.Heading;
            Locomotion.Stop();
            _contact = default;
            Motion = BodyMotion.Still;
            Costs?.Reset();
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
