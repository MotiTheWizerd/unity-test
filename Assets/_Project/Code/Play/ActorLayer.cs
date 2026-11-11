using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The physics layer actors' bodies are on (<see cref="ActorBody"/>: the player's, the world's characters'): they
    /// stand in each other's way and in nothing's look unless that look asks for them (<see cref="SceneLayers"/>). The
    /// project's tag manager names it; without it, bodies fall back to Ignore Raycast, which every look passes.
    /// </summary>
    public static class ActorLayer
    {
        public const string Name = "ForgeActors";

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
            Debug.LogError($"[Forge] Layer '{Name}' is missing from the project's tag manager; actors' bodies go on {Fallback} and walk through each other.");
            return LayerMask.NameToLayer(Fallback);
        }
    }
}
