using System;
using Forge.Game.Player;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The player's body as Unity's <see cref="CharacterController"/>: a capsule that collides with the terrain and the
    /// placed objects, walks up steps and slopes up to the profile's limits, and slides along walls. Its own scene object,
    /// on the Ignore Raycast layer so the editor's picks and the camera's depth probes look through it. Positions are the
    /// feet, in scene coordinates.
    /// </summary>
    public sealed class CharacterControllerMotor : IPlayerMotor, IDisposable
    {
        // A built-in layer, left out of Physics.DefaultRaycastLayers, which every pick in the editor uses.
        const string IgnoreRaycastLayer = "Ignore Raycast";

        readonly GameObject _body;
        readonly CharacterController _controller;

        public CharacterControllerMotor(PlayerProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            _body = new GameObject("Player") { layer = LayerMask.NameToLayer(IgnoreRaycastLayer) };
            _controller = _body.AddComponent<CharacterController>();
            _controller.height = profile.Height;
            _controller.radius = profile.Radius;
            // The transform is the feet: the capsule stands on it.
            _controller.center = new Vector3(0f, profile.Height * 0.5f, 0f);
            _controller.stepOffset = profile.StepHeight;
            _controller.slopeLimit = profile.SlopeLimit;
            _controller.skinWidth = Mathf.Max(0.01f, profile.Radius * 0.1f);
            _controller.minMoveDistance = 0f;
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

        public void Dispose()
        {
            if (_body != null)
                UnityEngine.Object.Destroy(_body);
        }
    }
}
