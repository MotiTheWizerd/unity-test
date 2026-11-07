using System.Collections.Generic;
using System.Numerics;
using Forge.Data;

namespace Forge.Game.Interaction
{
    /// <summary>
    /// What lies along an actor's line of sight, asked by its <see cref="InteractionFocus"/>, in the coordinates the actor
    /// stands in. The engine's physics answers it while playing; a fake does in tests.
    /// </summary>
    public interface IInteractionProbe
    {
        /// <summary>
        /// Adds to <paramref name="found"/> the placed objects the line from <paramref name="from"/> along
        /// <paramref name="direction"/> (a unit vector) for <paramref name="length"/> meters could mean, likeliest first:
        /// the one it hits, then one it only grazes, so a coin needn't be hit dead on. Nothing behind the first thing in the
        /// way (a wall, the ground) is found.
        /// </summary>
        void Find(Vector3 from, Vector3 direction, float length, IList<RecordId> found);
    }
}
