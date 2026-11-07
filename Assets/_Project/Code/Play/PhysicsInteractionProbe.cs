using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Game.Interaction;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The use key's line of sight through the engine's physics: the scene's colliders on the default raycast layers (the
    /// terrain and the placed objects, never the player's own body or the editor's gizmos), triggers ignored. What the line
    /// hits comes first, then what a ball of <c>aimRadius</c> rolled along it touches first, so small things needn't be hit
    /// dead on. A placed object is known by its <see cref="RecordLink"/>. Positions in scene coordinates.
    /// </summary>
    public sealed class PhysicsInteractionProbe : IInteractionProbe
    {
        readonly float _aimRadius;

        public PhysicsInteractionProbe(float aimRadius)
        {
            if (!(aimRadius >= 0f) || float.IsInfinity(aimRadius))
                throw new ArgumentOutOfRangeException(nameof(aimRadius), aimRadius, "The aim radius is zero or more meters.");
            _aimRadius = aimRadius;
        }

        public void Find(System.Numerics.Vector3 from, System.Numerics.Vector3 direction, float length, IList<RecordId> found)
        {
            var start = from.ToUnity();
            var way = direction.ToUnity();
            if (Physics.Raycast(start, way, out var hit, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                Add(hit.collider, found);
            if (_aimRadius > 0f
                && Physics.SphereCast(start, _aimRadius, way, out var graze, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                Add(graze.collider, found);
        }

        static void Add(Collider collider, IList<RecordId> found)
        {
            var link = collider.GetComponentInParent<RecordLink>();
            if (link != null && !found.Contains(link.RecordId))
                found.Add(link.RecordId);
        }
    }
}
