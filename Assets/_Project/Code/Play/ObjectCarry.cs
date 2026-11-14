using System;
using Forge.Game.Cameras;
using Forge.Game.Interaction;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// How an actor grabs and carries loose things, Skyrim's way: the physical body of a world object in reach (or of a
    /// part of something that may be carried though it is none, <see cref="Carriable"/>: a limp body's arm), along the
    /// middle of the view (<see cref="ReachRay"/>, a thin line then a ball, as the use key aims), no heavier than the actor
    /// can lift, is held a little way in front of its eyes, where it looks (<see cref="Carried"/>), one of two ways
    /// (<see cref="CarryHold"/>): loose (the use key held), by the point the line met, the rest hanging from there and
    /// swinging; or steady (the lift key held), by its middle, kept turned as it lay and turning as the actor turns. It
    /// knocks into things and walls stop it;
    /// held back too far for a while it is let go. Let go, it drops; thrown, it flies the way the actor looks, the lighter
    /// the faster. What is carried passes through the carrier, so it can't be stood on to ride it. Scene coordinates.
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
        float _heldBack;

        /// <param name="carrier">The carrier's own body, which what it carries passes through.</param>
        public ObjectCarry(Collider carrier, CarrySettings settings)
        {
            _carrier = carrier != null ? carrier : throw new ArgumentNullException(nameof(carrier));
            _settings = settings;
        }

        /// <summary>Whether something is carried now.</summary>
        public bool IsCarrying => _held != null;

        /// <summary>How what is carried is held; null when nothing is.</summary>
        public CarryHold? Holding => IsCarrying ? _held.Kind : (CarryHold?)null;

        /// <summary>
        /// Grabs what physical body of a world object is in reach along <paramref name="view"/> from eyes at
        /// <paramref name="eyes"/>, if it is light enough, held <paramref name="hold"/>; whether it did.
        /// </summary>
        public bool TryGrab(ViewPose view, Vector3 eyes, CarryHold hold)
        {
            if (IsCarrying || !ReachRay.TryGet(view, eyes.ToNumerics(), _settings.Reach, out var from, out var direction, out var length) ||
                !TryReach(from.ToUnity(), direction.ToUnity(), length, out var body, out var point) || body.mass > _settings.MostMass)
                return false;

            _heldColliders = body.GetComponentsInChildren<Collider>();
            foreach (var collider in _heldColliders)
                Physics.IgnoreCollision(_carrier, collider, true);
            _held = Carried.Take(body, point, hold, Facing(view, Quaternion.identity));
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
            _held.Facing = Facing(view, _held.Facing);
            _heldBack = _held.Gap > MostHeldBack ? _heldBack + Mathf.Max(0f, deltaTime) : 0f;
            if (_heldBack > HeldBackSeconds)
                Drop();
        }

        /// <summary>Lets go of what is carried if it is held <paramref name="hold"/> (the key that took it came up): it falls.</summary>
        public void Drop(CarryHold hold)
        {
            if (Holding == hold)
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

        /// <summary>
        /// Throws what is carried the way <paramref name="view"/> looks: the lighter, the faster; a part of something
        /// (a limp body's arm) goes with all it is joined to, as heavy as all of it.
        /// </summary>
        public void Throw(ViewPose view)
        {
            if (!IsCarrying)
                return;
            var body = _held.Body;
            Drop();
            if (body == null)
                return;
            var whole = body.TryGetComponent<Carriable>(out var part) ? part.Whole : null;
            var mass = body.mass;
            if (whole != null)
            {
                mass = 0f;
                foreach (var joined in whole)
                    mass += joined.mass;
            }

            var velocity = view.Forward.ToUnity() * Mathf.Min(_settings.ThrowSpeed, _settings.ThrowImpulse / Mathf.Max(mass, 0.01f));
            if (whole == null)
            {
                body.linearVelocity = velocity;
                return;
            }

            foreach (var joined in whole)
                joined.linearVelocity = velocity;
        }

        public void Dispose() => Drop();

        void Forget()
        {
            _held = null;
            _heldColliders = null;
        }

        // Which way the view faces, level (a turn about up alone); looking straight up or down it faces no way, so as before.
        static Quaternion Facing(ViewPose view, Quaternion before)
        {
            var forward = view.Forward.ToUnity();
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, Vector3.up) : before;
        }

        // The world object's own free body a line from start along way meets within length, else a ball of the aim's
        // radius (so small things are easy to take), and the point on it met; false when it meets none first (the
        // ground, a house, a held weapon).
        bool TryReach(Vector3 start, Vector3 way, float length, out Rigidbody body, out Vector3 point)
        {
            var layers = SceneLayers.World;
            if ((Physics.Raycast(start, way, out var hit, length, layers, QueryTriggerInteraction.Ignore) && IsFree(hit, out body)) ||
                (_settings.AimRadius > 0f && Physics.SphereCast(start, _settings.AimRadius, way, out hit, length, layers, QueryTriggerInteraction.Ignore) && IsFree(hit, out body)))
            {
                point = hit.point;
                return true;
            }

            body = null;
            point = default;
            return false;
        }

        // A loose body: a world object's, or one that may be carried though it is none (a part of a body gone limp).
        static bool IsFree(RaycastHit hit, out Rigidbody body)
        {
            body = hit.collider.attachedRigidbody;
            return body != null && !body.isKinematic && (body.GetComponent<RecordLink>() != null || body.GetComponent<Carriable>() != null);
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
