using System;
using System.Collections.Generic;
using Forge.Game.World;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// An actor's body kicking clutter aside as it moves (<see cref="ClutterKick"/>): what is on the
    /// <see cref="ClutterLayer"/> in its capsule, or just ahead of it, is sent on away from it. The capsule itself never
    /// meets clutter (its controller leaves the layer out), so a sword underfoot is no step and a cup no wall. What is
    /// being carried is its carrier's. Scene coordinates.
    /// </summary>
    public sealed class ClutterKicker
    {
        // How far beyond the capsule's skin clutter is kicked, meters: before it is touched, as a foot meets it first.
        const float Reach = 0.1f;

        readonly CharacterController _controller;
        readonly Collider[] _near = new Collider[32];
        readonly HashSet<Rigidbody> _kicked = new HashSet<Rigidbody>();

        public ClutterKicker(CharacterController controller) =>
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));

        /// <summary>Kicks what clutter the capsule has come to, it having moved at <paramref name="velocity"/> over <paramref name="deltaTime"/> seconds.</summary>
        public void Kick(Vector3 velocity, float deltaTime)
        {
            var mask = ClutterLayer.Mask;
            if (mask == 0 || !(deltaTime > 0f))
                return;

            var center = _controller.transform.TransformPoint(_controller.center);
            var half = Mathf.Max(0f, _controller.height * 0.5f - _controller.radius);
            var bottom = center + Vector3.down * half;
            var top = center + Vector3.up * half;
            var radius = _controller.radius + _controller.skinWidth + Reach;
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _near, mask, QueryTriggerInteraction.Ignore);
            _kicked.Clear();
            for (var i = 0; i < count; i++)
            {
                var collider = _near[i];
                var body = collider.attachedRigidbody;
                if (body != null && !body.isKinematic && !body.TryGetComponent<Carried>(out _) && _kicked.Add(body))
                    Kick(body, collider, bottom, top, radius, velocity, deltaTime);
            }
        }

        static void Kick(Rigidbody body, Collider collider, Vector3 bottom, Vector3 top, float radius, Vector3 velocity, float deltaTime)
        {
            var axis = ClosestOnSegment(bottom, top, collider.bounds.center);
            var surface = collider.ClosestPoint(axis);
            var away = surface - axis;
            // Right under the feet (or on the head), the nearest way out is level: out the side it lies to.
            if (away.x * away.x + away.z * away.z < 1e-6f)
                away = body.worldCenterOfMass - axis;
            var change = ClutterKick.VelocityChange(velocity.ToNumerics(), away.ToNumerics(), radius - Vector3.Distance(surface, axis),
                body.linearVelocity.ToNumerics(), deltaTime).ToUnity();
            if (change == Vector3.zero)
                return;
            // Set, not added as a force, so kicking again before the next physics step sends it no faster.
            body.WakeUp();
            body.linearVelocity += change;
        }

        static Vector3 ClosestOnSegment(Vector3 from, Vector3 to, Vector3 point)
        {
            var segment = to - from;
            var length = segment.sqrMagnitude;
            return length < 1e-8f ? from : from + segment * Mathf.Clamp01(Vector3.Dot(point - from, segment) / length);
        }
    }
}
