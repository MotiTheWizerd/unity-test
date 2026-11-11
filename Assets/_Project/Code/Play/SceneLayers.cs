using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// What the game's looks into the scene (rays, casts, overlaps) meet, in one place, so who sees whom is decided once:
    /// the world (<see cref="World"/>) or only its solid part (<see cref="Solid"/>). Actors' bodies
    /// (<see cref="ActorLayer"/>) are left out of both: no look starts by meeting its own body, a camera passes through
    /// people, and no blow or arrow reaches the world's characters yet.
    /// </summary>
    public static class SceneLayers
    {
        /// <summary>The world: terrain, placed objects, loose things and clutter; not actors.</summary>
        public static int World => Physics.DefaultRaycastLayers & ~ActorLayer.Mask;

        /// <summary>
        /// The world's solid part: the world but its clutter (<see cref="ClutterLayer"/>), which actors walk through and
        /// kick: what is stood on, what stands in a camera's way.
        /// </summary>
        public static int Solid => World & ~ClutterLayer.Mask;
    }
}
