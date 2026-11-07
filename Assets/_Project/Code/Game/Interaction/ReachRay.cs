using System;
using System.Numerics;
using Forge.Game.Cameras;

namespace Forge.Game.Interaction
{
    /// <summary>
    /// The stretch of a view's middle line an actor can reach, as Skyrim measures it: from level with the actor's eyes (so
    /// nothing between a third-person camera and the actor is picked) out to where the line leaves <c>reach</c> meters of
    /// the eyes. In first person the camera is at the eyes, so it is simply the first <c>reach</c> meters of the view.
    /// </summary>
    public static class ReachRay
    {
        /// <returns>False when the line never comes within <paramref name="reach"/> of <paramref name="eyes"/>.</returns>
        public static bool TryGet(ViewPose view, Vector3 eyes, float reach, out Vector3 from, out Vector3 direction, out float length)
        {
            from = default;
            length = 0f;
            direction = Vector3.Normalize(view.Forward);
            if (!(reach > 0f))
                return false;

            // Points along the line: camera + t × direction. Their distance to the eyes is within reach between the roots
            // of t² + 2bt + c = 0; the nearest point to the eyes is at t = -b.
            var toCamera = view.Position - eyes;
            var b = Vector3.Dot(direction, toCamera);
            var c = toCamera.LengthSquared() - reach * reach;
            var discriminant = b * b - c;
            if (!(discriminant > 0f))
                return false;

            var start = Math.Max(0f, -b);
            var end = -b + (float)Math.Sqrt(discriminant);
            if (!(end > start))
                return false;

            from = view.Position + direction * start;
            length = end - start;
            return true;
        }
    }
}
