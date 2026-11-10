using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Events;
using Forge.Game.Motion;

namespace Forge.Game.Player
{
    /// <summary>
    /// The player in the world: where it looks (<see cref="PlayerLook"/>), how it moves (<see cref="PlayerLocomotion"/>),
    /// its body (<see cref="IPlayerMotor"/>) and which way that body faces (<see cref="BodyHeading"/>, by
    /// <see cref="Facing"/>). Each frame it turns the look, works out the move, has the body make it, keeping what the
    /// body touched for the next frame, and turns the body. A body that can crouch (<see cref="ICrouchingBody"/>) crouches
    /// as the moves say, and stays down where there is no room to stand; the eyes come down with it. Engine-free;
    /// positions are the motor's.
    /// </summary>
    public sealed class PlayerCharacter
    {
        // Slower than this on the ground (m/s), the body keeps the way it faces rather than following jitter.
        const float TurnSpeedThreshold = 0.2f;

        // Seconds the eyes take to come down to a crouch, or back up.
        const float CrouchSeconds = 0.25f;

        readonly ICrouchingBody _crouching;
        readonly GameEvents _events;
        MotorContact _contact;

        /// <param name="eyeHeight">Meters from the feet to the eyes, where a first-person camera sits.</param>
        /// <param name="attributes">The player's attributes, which moving costs (<see cref="MovementCosts"/>) are paid from
        /// and which tick with it; null for a player whose moves cost nothing.</param>
        /// <param name="events">The game's stream, told what happens to the player (jumped, exhausted, fell); null to tell no one.</param>
        public PlayerCharacter(IPlayerMotor motor, LocomotionSettings locomotion, float eyeHeight, ActorAttributes attributes = null, GameEvents events = null)
        {
            Motor = motor ?? throw new ArgumentNullException(nameof(motor));
            _crouching = motor as ICrouchingBody;
            if (!(eyeHeight > 0f) || float.IsInfinity(eyeHeight))
                throw new ArgumentOutOfRangeException(nameof(eyeHeight), eyeHeight, "Must be a positive number.");
            EyeHeight = eyeHeight;
            Locomotion = new PlayerLocomotion(locomotion);
            _events = events;
            if (attributes != null)
                Costs = new MovementCosts(attributes, events);
        }

        /// <summary>What holds the player back whatever it is told (carrying too much), in order, before moving costs are paid.</summary>
        public IList<IMovementLimit> Limits { get; } = new List<IMovementLimit>();

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

        /// <summary>
        /// Whether it aims (draws a bow): the body turns to face where it looks, at the turn rate, whether it moves or
        /// not, as Skyrim's archer squares up to the shot; the game sets it each frame.
        /// </summary>
        public bool Aiming { get; set; }

        /// <summary>Compass degrees the body faces, which its look (model, animations) is turned to.</summary>
        public float BodyHeading { get; private set; }

        /// <summary>Where the feet are.</summary>
        public Vector3 Feet => Motor.Position;

        /// <summary>Where the eyes are, lowered as far as a crouch has brought them down.</summary>
        public Vector3 Eyes => Motor.Position + new Vector3(0f, EyeHeight - Lowered, 0f);

        /// <summary>Whether the body is crouched.</summary>
        public bool Crouched => _crouching != null && _crouching.Crouched;

        /// <summary>Meters the eyes are down now from standing, easing as the body crouches or stands (a camera follows).</summary>
        public float Lowered { get; private set; }

        /// <summary>Whether the last move ended standing on something.</summary>
        public bool IsGrounded => _contact.Grounded;

        /// <summary>How the body moved in the last frame, which its look (animations) shows.</summary>
        public BodyMotion Motion { get; private set; } = BodyMotion.Still;

        /// <summary>Plays one frame of <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(PlayerCommands commands, float deltaTime)
        {
            Look.Turn(commands.Turn);
            foreach (var limit in Limits)
                commands = limit.Allow(commands);
            commands = Headroom(commands);
            if (Costs != null)
                commands = Costs.Afford(commands, deltaTime);
            var from = Motor.Position;
            var move = Locomotion.Step(commands, Look.Heading, _contact, deltaTime);
            if (_crouching != null && _crouching.Crouched != Locomotion.Crouching)
                _crouching.Crouch(Locomotion.Crouching);
            if (move != Vector3.Zero)
                _contact = Motor.Move(move);
            if (deltaTime > 0f)
            {
                Motion = BodyMotion.Between(from, Motor.Position, deltaTime, _contact.Grounded, Crouched);
                Costs?.Pay(Locomotion.Jumped, _contact.Grounded, Motor.Position.Y);
                if (Locomotion.Jumped)
                    _events?.Publish(new ActorJumped(Attributes?.Owner ?? ActorId.Player));
                Attributes?.Tick(deltaTime);
                LowerEyes(deltaTime);
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
            Lowered = CrouchedEyeDrop;
            Costs?.Reset();
        }

        // A body that can't crouch never does; one crouched with no room overhead stays down, whatever it is told: it
        // neither stands, nor sprints, nor jumps.
        PlayerCommands Headroom(PlayerCommands commands)
        {
            if (_crouching == null)
                return commands.AllowingCrouchToggle(false);
            if (Locomotion.Crouching && !_crouching.CanStand)
                return commands.Allowing(sprint: false, jump: false, walk: commands.Walk).AllowingCrouchToggle(false);
            return commands;
        }

        // How far down the eyes are with the body as it is: its crouch's depth, or none.
        float CrouchedEyeDrop => Crouched ? _crouching.CrouchDepth : 0f;

        // The eyes ease down to a crouch, or up, at the depth over CrouchSeconds a second.
        void LowerEyes(float deltaTime)
        {
            var target = CrouchedEyeDrop;
            var step = (_crouching?.CrouchDepth ?? 0f) / CrouchSeconds * deltaTime;
            Lowered = Math.Abs(target - Lowered) <= step ? target : Lowered + Math.Sign(target - Lowered) * step;
        }

        void TurnBody(float deltaTime)
        {
            if (Facing == BodyFacing.Look)
            {
                BodyHeading = Look.Heading;
                return;
            }

            if (Aiming)
            {
                if (deltaTime > 0f)
                    BodyHeading = TurnToward(BodyHeading, Look.Heading, Locomotion.Settings.TurnRate * deltaTime);
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
