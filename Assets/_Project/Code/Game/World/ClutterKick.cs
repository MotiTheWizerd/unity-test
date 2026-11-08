using System;
using System.Numerics;

namespace Forge.Game.World
{
    /// <summary>
    /// How an actor's body kicks the clutter it walks into (<see cref="PhysicalObjects.IsClutter"/>), as Skyrim's cups
    /// and baskets skitter ahead of the feet: what it touches is sent on away from it, level, a little faster than the
    /// actor comes on that way, and what it overlaps while standing is eased out of it. Something already going away
    /// faster is left be, and nothing is ever pulled toward the actor. Engine-free.
    /// </summary>
    public static class ClutterKick
    {
        /// <summary>How much faster than the actor comes on a kicked thing goes: it skitters ahead rather than riding the shin.</summary>
        public const float Overtake = 1.25f;

        /// <summary>The fastest a kick sends anything, meters a second: a sprint scatters clutter, it doesn't shoot it.</summary>
        public const float MostSpeed = 6f;

        /// <summary>The fastest something inside the body is eased out of it, meters a second.</summary>
        public const float MostEasing = 2f;

        /// <summary>
        /// The change of velocity that kicks a thing <paramref name="depth"/> meters into an actor's body (less when it
        /// only comes near) along <paramref name="away"/>, from the body toward the thing (only its level part counts),
        /// the actor moving at <paramref name="actorVelocity"/> and the thing at <paramref name="velocity"/>, over a
        /// step of <paramref name="deltaTime"/> seconds. Zero when nothing need change.
        /// </summary>
        public static Vector3 VelocityChange(Vector3 actorVelocity, Vector3 away, float depth, Vector3 velocity, float deltaTime)
        {
            var level = new Vector3(away.X, 0f, away.Z);
            if (level.LengthSquared() < 1e-8f || !(deltaTime > 0f))
                return Vector3.Zero;
            level = Vector3.Normalize(level);

            var coming = Math.Max(0f, Vector3.Dot(actorVelocity, level)) * Overtake;
            var easing = Math.Min(Math.Max(0f, depth) / deltaTime, MostEasing);
            var wanted = Math.Min(Math.Max(coming, easing), MostSpeed);
            var going = Vector3.Dot(velocity, level);
            return going >= wanted ? Vector3.Zero : level * (wanted - going);
        }
    }
}
