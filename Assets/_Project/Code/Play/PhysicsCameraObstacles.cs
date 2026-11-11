using Forge.Game.Cameras;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The camera's obstacles are the scene's colliders on the default raycast layers: the terrain and the placed
    /// objects, never the player's own body (Ignore Raycast), the editor's gizmos or clutter (<see cref="ClutterLayer"/>:
    /// a cup kicked past the lens is no wall). Positions in scene coordinates.
    /// </summary>
    public sealed class PhysicsCameraObstacles : ICameraObstacles
    {
        public float Clearance(System.Numerics.Vector3 from, System.Numerics.Vector3 to, float radius)
        {
            var start = from.ToUnity();
            var path = to.ToUnity() - start;
            var length = path.magnitude;
            if (length < 1e-5f)
                return 0f;

            var layers = SceneLayers.Solid;
            // A ball that starts inside something can't move at all.
            if (Physics.CheckSphere(start, radius, layers, QueryTriggerInteraction.Ignore))
                return 0f;
            return Physics.SphereCast(start, radius, path / length, out var hit, length, layers, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : length;
        }
    }
}
