using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The physics layer of what a blow or a shot meets of an actor (<see cref="Hitbox"/>, <see cref="GuardFace"/>): only
    /// a look that asks for it finds it (<see cref="SceneLayers.Strikes"/>), so a camera, a reach for an item or an aim
    /// passes through. The project's tag manager names it; without it, they fall back to Ignore Raycast, where only a look
    /// that asks for that layer finds them.
    /// </summary>
    public static class HitboxLayer
    {
        public const string Name = "ForgeHitboxes";

        // A built-in layer, left out of Physics.DefaultRaycastLayers.
        const string Fallback = "Ignore Raycast";

        static int? _index;

        /// <summary>The layer's index (Ignore Raycast's when the project has none).</summary>
        public static int Index => _index ??= Find();

        /// <summary>The layer as a mask.</summary>
        public static int Mask => 1 << Index;

        static int Find()
        {
            var index = LayerMask.NameToLayer(Name);
            if (index >= 0)
                return index;
            Debug.LogError($"[Forge] Layer '{Name}' is missing from the project's tag manager; hitboxes go on {Fallback}.");
            return LayerMask.NameToLayer(Fallback);
        }
    }
}
