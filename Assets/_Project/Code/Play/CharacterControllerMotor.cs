using System;
using Forge.Game.Player;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The player's body as Unity's <see cref="CharacterController"/>: a capsule that collides with the terrain and the
    /// placed objects, walks up steps and slopes up to the profile's limits, and slides along walls. Clutter it walks
    /// through, kicking it aside (<see cref="ClutterKicker"/>). Crouched, the capsule is the profile's crouch height, its
    /// feet where they were, and it stands back up only when the room above it is clear (<see cref="ICrouchingBody"/>).
    /// Its own scene object, on the Ignore Raycast layer so the editor's picks and the camera's depth probes look through
    /// it. Positions are the feet, in scene coordinates.
    /// </summary>
    public sealed class CharacterControllerMotor : IPlayerMotor, ICrouchingBody, IDisposable
    {
        // A built-in layer, left out of Physics.DefaultRaycastLayers, which every pick in the editor uses.
        const string IgnoreRaycastLayer = "Ignore Raycast";

        readonly GameObject _body;
        readonly CharacterController _controller;
        readonly ClutterKicker _kicker;
        readonly float _standingHeight;
        readonly float _crouchedHeight;
        // What blocks standing up: anything solid, but the body itself and the clutter it walks through.
        readonly int _overhead;

        public CharacterControllerMotor(PlayerProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            _body = new GameObject("Player") { layer = LayerMask.NameToLayer(IgnoreRaycastLayer) };
            _controller = _body.AddComponent<CharacterController>();
            _standingHeight = profile.Height;
            _crouchedHeight = profile.CrouchHeight;
            _overhead = Physics.DefaultRaycastLayers & ~ClutterLayer.Mask;
            _controller.radius = profile.Radius;
            Stand(_standingHeight);
            _controller.stepOffset = profile.StepHeight;
            _controller.slopeLimit = profile.SlopeLimit;
            _controller.skinWidth = Mathf.Max(0.01f, profile.Radius * 0.1f);
            _controller.minMoveDistance = 0f;
            // Clutter is never stood on or walked into: it is kicked aside.
            _controller.excludeLayers = ClutterLayer.Mask;
            _kicker = new ClutterKicker(_controller);
        }

        /// <summary>How far the feet stay above the ground they stand on: the controller's skin, meters.</summary>
        public float Hover => _controller.skinWidth;

        /// <summary>The body's scene object, whose transform is the feet (what the player's model rides on).</summary>
        public Transform Body => _body.transform;

        /// <summary>The body's collider, the capsule: what the player carries passes through it.</summary>
        public Collider Collider => _controller;

        public System.Numerics.Vector3 Position => _body.transform.position.ToNumerics();

        public MotorContact Move(System.Numerics.Vector3 displacement)
        {
            var flags = _controller.Move(displacement.ToUnity());
            _kicker.Kick(_controller.velocity, Time.deltaTime);
            return new MotorContact(
                grounded: (flags & CollisionFlags.Below) != 0 || _controller.isGrounded,
                hitCeiling: (flags & CollisionFlags.Above) != 0);
        }

        // A character controller keeps its own idea of where it is; moved by hand, it must be switched off meanwhile.
        public void Teleport(System.Numerics.Vector3 feet)
        {
            _controller.enabled = false;
            _body.transform.position = feet.ToUnity();
            _controller.enabled = true;
        }

        public bool Crouched { get; private set; }

        public float CrouchDepth => _standingHeight - _crouchedHeight;

        // The room the standing capsule would take above the crouched one, a skin narrower so a wall it leans on
        // doesn't count.
        public bool CanStand
        {
            get
            {
                if (!Crouched)
                    return true;
                var feet = _body.transform.position;
                var radius = _controller.radius - _controller.skinWidth;
                return !Physics.CheckCapsule(
                    feet + Vector3.up * (_crouchedHeight - _controller.radius),
                    feet + Vector3.up * (_standingHeight - _controller.radius),
                    radius, _overhead, QueryTriggerInteraction.Ignore);
            }
        }

        public void Crouch(bool crouched)
        {
            Crouched = crouched;
            Stand(crouched ? _crouchedHeight : _standingHeight);
        }

        // The capsule `height` tall, standing on the transform: the feet.
        void Stand(float height)
        {
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        public void Dispose()
        {
            if (_body != null)
                UnityEngine.Object.Destroy(_body);
        }
    }
}
