using System.Numerics;
using Forge.Data.Records;

namespace Forge.Game.Items
{
    /// <summary>
    /// Where an item set down in the world comes to rest, in world coordinates: whatever plays the world finds the ground
    /// and seats the item on it the way it seats every object, so a dropped sword lies like a placed one.
    /// </summary>
    public interface IItemPlacement
    {
        /// <summary>
        /// The pose of <paramref name="item"/> set down below <paramref name="above"/>, turned to
        /// <paramref name="heading"/> (compass degrees); false when there is nothing below to rest on.
        /// </summary>
        bool TryRest(BaseObjectRecord item, Vector3 above, float heading, out Vector3 position, out Quaternion rotation);
    }
}
