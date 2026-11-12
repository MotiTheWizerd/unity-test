using Forge.Game.Minds;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Line of sight through the engine's physics: clear when nothing solid of the world's
    /// (<see cref="SceneLayers.Solid"/>: the terrain, walls, rocks, trees; not clutter, not people) stands between two
    /// points, triggers ignored. Scene coordinates.
    /// </summary>
    public sealed class PhysicsLineOfSight : ILineOfSight
    {
        public bool Clear(System.Numerics.Vector3 from, System.Numerics.Vector3 to) =>
            !Physics.Linecast(from.ToUnity(), to.ToUnity(), SceneLayers.Solid, QueryTriggerInteraction.Ignore);
    }
}
