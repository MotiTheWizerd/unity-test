using System;
using Forge.Game.Cameras;
using Forge.Game.Interaction;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// How an actor grabs and carries loose things, Skyrim's way (the use key held): the physical body of a world object
    /// in reach, along the middle of the view (<see cref="ReachRay"/>, a thin line then a ball, as the use key aims), no
    /// heavier than the actor can lift, is held a little way in front of its eyes, where it looks, turned as the actor
    /// turns (<see cref="Carried"/>). It knocks into things and walls stop it; held back too far for a while it is let go. Let go, it
    /// drops; thrown, it flies the way the actor looks, the lighter the faster. What is carried passes through the
    /// carrier, so it can't be stood on to ride it. Scene coordinates.
    /// </summary>
    public sealed class ObjectCarry : IDisposable
    {
        // How far a carried thing may be kept from where it is held, meters, and for how long, seconds, before it is let
        // go: stuck behind something, not merely catching up with a quick turn or just picked up.
        const float MostHeldBack = 1f;
        const float HeldBackSeconds = 0.5f;

        readonly Collider _carrier;
        readonly CarrySettings _settings;
        Carried _held;
        Collider[] _heldColliders;
        Quaternion _turnInView;
        float _heldBack;

        /// <param name="carrier">The carrier's own body, which what it carries passes through.</param>
        public ObjectCarry(Collider carrier, CarrySettings settings)
        {
            _carrier = carrier != null ? carrier : throw new ArgumentNullException(nameof(carrier));
            _settings = settings;
        }

        /// <summary>Whether something is carried now.</summary>
        public bool IsCarrying => _held != null;

        /// <summary>
        /// Grabs what physical body of a world object is in reach along <paramref name="view"/> from eyes at
        /// <paramref name="eyes"/>, if it is light enough; whether it did.
        /// </summary>
        public bool TryGrab(ViewPose view, Vector3 eyes)
        {
            if (IsCarrying || !ReachRay.TryGet(view, eyes.ToNumerics(), _settings.Reach, out var from, out var direction, out var length))
                return false;
            var start = from.ToUnity();
            var way = direction.ToUnity();
            var body = BodyHit(Physics.Raycast(start, way, out var hit, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit : (RaycastHit?)null)
                       ?? (_settings.AimRadius > 0f && Physics.SphereCast(start, _settings.AimRadius, way, out var graze, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                           ? BodyHit(graze)
                           : null);
            if (body == null || body.mass > _settings.MostMass)
                return false;

            _heldColliders = body.GetComponentsInChildren<Collider>();
            foreach (var collider in _heldColliders)
                Physics.IgnoreCollision(_carrier, collider, true);
            _held = body.gameObject.AddComponent<Carried>();
            _turnInView = Quaternion.Inverse(Level(view)) * body.rotation;
            _heldBack = 0f;
            Hold(view, eyes, 0f);
            return true;
        }

        /// <summary>
        /// Keeps what is carried where it is held for this frame's <paramref name="view"/>, <paramref name="deltaTime"/>
        /// seconds after the last; lets it go when it has been held back too far for too long.
        /// </summary>
        public void Hold(ViewPose view, Vector3 eyes, float deltaTime)
        {
            if (!IsCarrying)
                return;
            if (_held.Body == null)
            {
                Forget();
                return;
            }

            _held.Target = eyes + view.Forward.ToUnity() * _settings.HoldDistance;
            _held.TargetRotation = Level(view) * _turnInView;
            _heldBack = _held.Gap > MostHeldBack ? _heldBack + Mathf.Max(0f, deltaTime) : 0f;
            if (_heldBack > HeldBackSeconds)
                Drop();
        }

        /// <summary>Lets go of what is carried: it falls.</summary>
        public void Drop()
        {
            if (!IsCarrying)
                return;
            var body = _held.Body;
            UnityEngine.Object.DestroyImmediate(_held);
            if (_heldColliders != null)
            {
                foreach (var collider in _heldColliders)
                {
                    if (collider != null)
                        Physics.IgnoreCollision(_carrier, collider, false);
                }
            }

            Forget();
            if (body != null)
                body.WakeUp();
        }

        /// <summary>Throws what is carried the way <paramref name="view"/> looks: the lighter, the faster.</summary>
        public void Throw(ViewPose view)
        {
            if (!IsCarrying)
                return;
            var body = _held.Body;
            Drop();
            if (body == null)
                return;
            var speed = Mathf.Min(_settings.ThrowSpeed, _settings.ThrowImpulse / Mathf.Max(body.mass, 0.01f));
            body.linearVelocity = view.Forward.ToUnity() * speed;
        }

        public void Dispose() => Drop();

        void Forget()
        {
            _held = null;
            _heldColliders = null;
        }

        // A world object's own free body that a line or ball met, or null (the ground, a house, a held weapon).
        static Rigidbody BodyHit(RaycastHit? hit)
        {
            var body = hit?.collider.attachedRigidbody;
            return body != null && !body.isKinematic && body.GetComponent<RecordLink>() != null ? body : null;
        }

        // The view turned only about the up axis: what is carried turns as the actor turns, not as it looks up or down.
        static Quaternion Level(ViewPose view)
        {
            var forward = view.Forward.ToUnity();
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
        }
    }

    /// <summary>How an actor grabs and carries loose things (<see cref="ObjectCarry"/>): a player's are its profile's.</summary>
    public readonly struct CarrySettings
    {
        /// <param name="reach">How far from the eyes it grabs, meters.</param>
        /// <param name="aimRadius">How near the middle of the view a thing need only be, meters.</param>
        /// <param name="holdDistance">How far in front of the eyes it holds what it carries, meters.</param>
        /// <param name="mostMass">The heaviest it can lift, kilograms.</param>
        /// <param name="throwImpulse">How hard it throws, newton-seconds: what it throws flies at this over its mass...</param>
        /// <param name="throwSpeed">...up to this, meters a second.</param>
        public CarrySettings(float reach, float aimRadius, float holdDistance, float mostMass, float throwImpulse, float throwSpeed)
        {
            Reach = reach;
            AimRadius = aimRadius;
            HoldDistance = holdDistance;
            MostMass = mostMass;
            ThrowImpulse = throwImpulse;
            ThrowSpeed = throwSpeed;
        }

        public float Reach { get; }

        public float AimRadius { get; }

        public float HoldDistance { get; }

        public float MostMass { get; }

        public float ThrowImpulse { get; }

        public float ThrowSpeed { get; }
    }
}
