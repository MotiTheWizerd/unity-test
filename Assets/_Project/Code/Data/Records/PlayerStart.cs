using System;
using System.Numerics;

namespace Forge.Data.Records
{
    /// <summary>
    /// Where a game begins in a space, as a Bethesda game's player start marker: the feet's position (local to the
    /// space) and the compass heading the player faces. A space without one begins wherever its player is put.
    /// </summary>
    public sealed class PlayerStart
    {
        /// <param name="position">Where the feet stand, local to the space.</param>
        /// <param name="heading">Compass degrees faced: 0 north (+Z), 90 east (+X).</param>
        public PlayerStart(Vector3 position, float heading)
        {
            if (!IsFinite(position.X) || !IsFinite(position.Y) || !IsFinite(position.Z))
                throw new ArgumentOutOfRangeException(nameof(position), position, "A start's position must be finite.");
            if (!IsFinite(heading))
                throw new ArgumentOutOfRangeException(nameof(heading), heading, "A start's heading must be a finite number of degrees.");

            Position = position;
            Heading = heading;
        }

        public Vector3 Position { get; }

        public float Heading { get; }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
