using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// What the game's looks into the scene (rays, casts, overlaps) meet, in one place, so who sees whom is decided once:
    /// the world (<see cref="World"/>), only its solid part (<see cref="Solid"/>), or the world and the actors standing
    /// in it (<see cref="Strikes"/>, <see cref="Targets"/>). Actors' bodies (<see cref="ActorLayer"/>) and their hitboxes
    /// (<see cref="HitboxLayer"/>) are left out of the first two: no look starts by meeting its own body, and a camera
    /// passes through people. A blow or an arrow asks for the actors, and passes over the striker's own by its name
    /// (<see cref="ActorLink"/>, <see cref="GameStage.Identify"/>).
    /// </summary>
    public static class SceneLayers
    {
        /// <summary>The world: terrain, placed objects, loose things and clutter; not actors.</summary>
        public static int World => Physics.DefaultRaycastLayers & ~ActorLayer.Mask & ~HitboxLayer.Mask;

        /// <summary>
        /// The world's solid part: the world but its clutter (<see cref="ClutterLayer"/>), which actors walk through and
        /// kick: what is stood on, what stands in a camera's way.
        /// </summary>
        public static int Solid => World & ~ClutterLayer.Mask;

        /// <summary>The actors' bodies alone (<see cref="ActorLayer"/>): the capsules they walk with.</summary>
        public static int Actors => ActorLayer.Mask;

        /// <summary>What a blow or a shot meets of the actors: their bodies' parts (<see cref="Hitbox"/>) and their raised guards (<see cref="GuardFace"/>).</summary>
        public static int Hitboxes => HitboxLayer.Mask;

        /// <summary>
        /// What a shot can meet: the world, and the actors standing in it where they are drawn (<see cref="Hitboxes"/>),
        /// not the capsules they walk with; the shooter's own among them (whoever shoots passes over them).
        /// </summary>
        public static int Strikes => World | Hitboxes;

        /// <summary>
        /// What a blow can meet: the world, the actors' bodies by the capsules they walk with, and their raised guards; the
        /// striker's own among them (whoever strikes passes over them).
        /// </summary>
        public static int Targets => World | Actors | Hitboxes;
    }
}
