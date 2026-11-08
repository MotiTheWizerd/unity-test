using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A body being carried (<see cref="ObjectCarry"/>): every physics step it is driven toward where the carrier holds
    /// it (<see cref="Target"/>), turned as the carrier turns, by setting its speed rather than its place, so it still
    /// knocks into things and is stopped by walls. Weightless while held. Taken off, it falls again.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Carried : MonoBehaviour
    {
        // How fast a carried thing goes at most, meters a second, and how fast it turns, degrees a second: quick enough
        // to keep up with a turn of the head, slow enough not to fling what it hits.
        const float MostSpeed = 12f;
        const float MostTurn = 720f;

        // The part of the way to the target closed each second, so it eases in rather than overshoots.
        const float Catch = 15f;

        Rigidbody _rigidbody;
        bool _usedGravity;
        RigidbodyInterpolation _interpolation;

        /// <summary>Where it is held, scene space.</summary>
        public Vector3 Target { get; set; }

        /// <summary>How it is turned while held, scene space.</summary>
        public Quaternion TargetRotation { get; set; }

        /// <summary>How far it is from where it is held, meters: what a wall between keeps it from.</summary>
        public float Gap => _rigidbody != null ? Vector3.Distance(_rigidbody.worldCenterOfMass, Target) : 0f;

        /// <summary>The body.</summary>
        public Rigidbody Body => _rigidbody;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _usedGravity = _rigidbody.useGravity;
            _interpolation = _rigidbody.interpolation;
            _rigidbody.useGravity = false;
            // Seen moving every frame, it is drawn between physics steps, not stepping.
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.WakeUp();
            Target = _rigidbody.worldCenterOfMass;
            TargetRotation = _rigidbody.rotation;
        }

        void FixedUpdate()
        {
            var velocity = (Target - _rigidbody.worldCenterOfMass) * Catch;
            _rigidbody.linearVelocity = Vector3.ClampMagnitude(velocity, MostSpeed);

            (TargetRotation * Quaternion.Inverse(_rigidbody.rotation)).ToAngleAxis(out var angle, out var axis);
            if (angle > 180f)
                angle -= 360f;
            var turn = float.IsInfinity(axis.x) || Mathf.Approximately(angle, 0f)
                ? Vector3.zero
                : axis * Mathf.Clamp(angle * Catch, -MostTurn, MostTurn) * Mathf.Deg2Rad;
            _rigidbody.angularVelocity = turn;
        }

        void OnDestroy()
        {
            if (_rigidbody == null)
                return;
            _rigidbody.useGravity = _usedGravity;
            _rigidbody.interpolation = _interpolation;
        }
    }
}
