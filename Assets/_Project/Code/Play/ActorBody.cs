using System;
using Forge.Game.Actors;
using Forge.Game.Motion;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// An actor's body, the player's or a character's of the world, as Unity's <see cref="CharacterController"/>: a
    /// capsule of its <see cref="BodyShape"/> that collides with the terrain, the placed objects and other actors' bodies,
    /// walks up steps and slopes up to its limits, and slides along walls. Clutter it walks through, kicking it aside
    /// (<see cref="ClutterKicker"/>). Crouched, the capsule is its crouch height, its feet where they were, and it stands
    /// back up only when the room above it is clear (<see cref="ICrouchingBody"/>). Its own scene object, on the
    /// <see cref="ActorLayer"/>, which looks into the scene leave out unless they ask for actors (<see cref="SceneLayers"/>:
    /// a blow, an arrow), tagged with whose it is (<see cref="ActorLink"/>). Guarding (<see cref="Guard"/>: its weapons in
    /// hand) the capsule is its shape's guard wide, so two fighters' guards meet rather than sink into each other. Lying
    /// (<see cref="Lie"/>: fallen), the capsule is switched off where the feet are, so nothing bumps into it and no blow
    /// finds it, until it stands again. Positions are the feet, in scene coordinates.
    /// </summary>
    public sealed class ActorBody : IMotor, ICrouchingBody, IDisposable
    {
        readonly GameObject _body;
        readonly CharacterController _controller;
        readonly ClutterKicker _kicker;
        readonly float _standingHeight;
        readonly float _crouchedHeight;
        readonly float _radius;
        readonly float _guardRadius;

        /// <param name="name">Its scene object's name ("Player", the character's).</param>
        /// <param name="actor">Whose body it is, for whatever meets it.</param>
        public ActorBody(string name, BodyShape shape, ActorId actor)
        {
            _body = new GameObject(name) { layer = ActorLayer.Index };
            _body.AddComponent<ActorLink>().Actor = actor;
            _controller = _body.AddComponent<CharacterController>();
            _standingHeight = shape.Height;
            _crouchedHeight = shape.CrouchHeight;
            _radius = shape.Radius;
            _guardRadius = shape.GuardRadius;
            _controller.radius = shape.Radius;
            Stand(_standingHeight);
            _controller.stepOffset = shape.StepHeight;
            _controller.slopeLimit = shape.SlopeLimit;
            _controller.skinWidth = Mathf.Max(0.01f, shape.Radius * 0.1f);
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
            // Lying, it goes nowhere: the capsule is off (a controller moved while off only complains).
            if (Lying)
                return new MotorContact(grounded: true, hitCeiling: false);
            var flags = _controller.Move(displacement.ToUnity());
            _kicker.Kick(_controller.velocity, Time.deltaTime);
            return new MotorContact(
                grounded: (flags & CollisionFlags.Below) != 0 || _controller.isGrounded,
                hitCeiling: (flags & CollisionFlags.Above) != 0);
        }

        // A character controller keeps its own idea of where it is; moved by hand, it must be switched off meanwhile
        // (and stays off while it lies).
        public void Teleport(System.Numerics.Vector3 feet)
        {
            _controller.enabled = false;
            _body.transform.position = feet.ToUnity();
            _controller.enabled = !Lying;
        }

        /// <summary>Whether it holds its guard (its weapons in hand): as wide as its shape's guard.</summary>
        public bool Guarding { get; private set; }

        /// <summary>
        /// Holds its guard (<paramref name="up"/>: its weapons in hand), the capsule as wide as its shape's guard
        /// (<see cref="BodyShape.GuardRadius"/>), so others keep clear of the shield before it; or lowers it, as wide as
        /// itself again.
        /// </summary>
        public void Guard(bool up)
        {
            Guarding = up;
            _controller.radius = up ? _guardRadius : _radius;
        }

        /// <summary>Whether it lies (fallen): no obstacle to anyone, and struck by nothing, until it stands again.</summary>
        public bool Lying { get; private set; }

        /// <summary>
        /// Lies down where it is (<paramref name="lying"/>: the capsule switched off, so a corpse is walked over and a blow
        /// passes through it), or stands back up (on again, where the feet are).
        /// </summary>
        public void Lie(bool lying)
        {
            Lying = lying;
            _controller.enabled = !lying;
        }

        public bool Crouched { get; private set; }

        public float CrouchDepth => _standingHeight - _crouchedHeight;

        // The room the standing capsule would take above the crouched one, a skin narrower so a wall it leans on
        // doesn't count; the world's solid things only, so neither its own body nor another's keeps it down.
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
                    radius, SceneLayers.Solid, QueryTriggerInteraction.Ignore);
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
