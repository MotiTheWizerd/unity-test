using System;
using System.Numerics;
using Forge.Core.Geometry;

namespace Forge.Game.Player
{
    /// <summary>
    /// The player's movement rules: given the commands, which way it faces and what the body touched last, how far to
    /// move this frame. On the ground the pace eases toward the wanted one quickly and the body is held down so it stays
    /// on downhill slopes; in the air gravity pulls and steering is weak. A jump needs the ground, or a moment after
    /// leaving it (coyote time), and only one per landing. Engine-free: the motor (<see cref="IPlayerMotor"/>) carries
    /// the move out.
    /// </summary>
    public sealed class PlayerLocomotion
    {
        /// <summary>Downward speed while standing, so each move presses into the ground and the body keeps touching it.</summary>
        public const float GroundStickSpeed = 2f;

        readonly LocomotionSettings _settings;
        float _sinceGrounded = float.PositiveInfinity;
        bool _jumped;

        public PlayerLocomotion(LocomotionSettings settings = null) => _settings = settings ?? LocomotionSettings.Default;

        public LocomotionSettings Settings => _settings;

        /// <summary>Meters per second, world axes (y up).</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>Walking rather than running, until switched back; sprint still outruns both.</summary>
        public bool Walking { get; private set; }

        /// <summary>Whether the last step took off in a jump.</summary>
        public bool Jumped { get; private set; }

        /// <summary>
        /// The move for one frame of <paramref name="deltaTime"/> seconds, facing <paramref name="heading"/> (compass
        /// degrees), after a move that touched <paramref name="contact"/>.
        /// </summary>
        public Vector3 Step(PlayerCommands commands, float heading, MotorContact contact, float deltaTime)
        {
            if (commands.ToggleWalk)
                Walking = !Walking;
            Jumped = false;
            if (!(deltaTime > 0f))
                return Vector3.Zero;

            if (contact.Grounded)
            {
                _sinceGrounded = 0f;
                _jumped = false;
            }
            else
            {
                _sinceGrounded += deltaTime;
            }

            var velocity = Velocity;
            var wanted = Wanted(commands, heading);
            var acceleration = contact.Grounded ? _settings.GroundAcceleration : _settings.AirAcceleration;
            var ground = MoveTowards(new Vector2(velocity.X, velocity.Z), wanted, acceleration * deltaTime);

            float up;
            if (commands.Jump && !_jumped && _sinceGrounded <= _settings.CoyoteTime)
            {
                up = _settings.JumpSpeed;
                _jumped = true;
                Jumped = true;
            }
            else if (contact.Grounded && velocity.Y <= 0f)
            {
                up = -GroundStickSpeed;
            }
            else
            {
                // A head against a ceiling stops rising at once.
                var rising = contact.HitCeiling ? Math.Min(velocity.Y, 0f) : velocity.Y;
                up = Math.Max(rising - _settings.Gravity * deltaTime, -_settings.MaxFallSpeed);
            }

            Velocity = new Vector3(ground.X, up, ground.Y);
            return Velocity * deltaTime;
        }

        /// <summary>Stands still: no speed, no jump in progress (after a teleport, say). Walking or running stays as chosen.</summary>
        public void Stop()
        {
            Velocity = Vector3.Zero;
            _sinceGrounded = float.PositiveInfinity;
            _jumped = false;
        }

        // The ground velocity the commands ask for: the stick or keys turned by the heading, never faster than the pace.
        Vector2 Wanted(PlayerCommands commands, float heading)
        {
            var move = commands.Move;
            var length = move.Length();
            if (!(length > 1e-4f))
                return Vector2.Zero;
            if (length > 1f)
                move /= length;

            var pace = commands.Sprint ? _settings.SprintSpeed : Walking || commands.Walk ? _settings.WalkSpeed : _settings.RunSpeed;
            return (Compass.Right(heading) * move.X + Compass.Forward(heading) * move.Y) * pace;
        }

        static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxStep)
        {
            var difference = target - current;
            var distance = difference.Length();
            return distance <= maxStep || distance < 1e-6f ? target : current + difference / distance * maxStep;
        }
    }
}
