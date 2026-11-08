using Forge.Game.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The physics layer clutter is on while the world plays (<see cref="PhysicalObjects.IsClutter"/>, put there by
    /// <see cref="WorldPhysics"/>): actors' bodies leave it out and kick it aside instead (<see cref="ClutterKicker"/>), and
    /// the camera looks past it. The project's tag manager names it; without it, clutter stays on its own layers and
    /// stands in the way as before.
    /// </summary>
    public static class ClutterLayer
    {
        public const string Name = "ForgeClutter";

        static int? _index;

        /// <summary>The layer's index; -1 when the project has none.</summary>
        public static int Index => _index ??= Find();

        /// <summary>The layer as a mask; empty when the project has none.</summary>
        public static int Mask => Index < 0 ? 0 : 1 << Index;

        static int Find()
        {
            var index = LayerMask.NameToLayer(Name);
            if (index < 0)
                Debug.LogError($"[Forge] Layer '{Name}' is missing from the project's tag manager; clutter will stand in the way.");
            return index;
        }
    }
}
