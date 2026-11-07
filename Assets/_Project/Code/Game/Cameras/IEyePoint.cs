using System.Numerics;

namespace Forge.Game.Cameras
{
    /// <summary>
    /// Where the player's eyes are as its look animates (the head swaying with a stride, leaning into a blow, lower in a
    /// fighting stance), so a first-person camera rides them rather than a fixed height. Engine-free; the look's side
    /// reads it off the head.
    /// </summary>
    public interface IEyePoint
    {
        /// <summary>
        /// The eyes from the feet, in the body's own frame (x to its right, y up, z ahead as it faces), meters; false when
        /// it can't tell now.
        /// </summary>
        bool TryGetEyes(out Vector3 eyes);
    }
}
