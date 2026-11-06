using System;

namespace Forge.Game.Motion
{
    /// <summary>
    /// How far a body fell: the highest it got while off the ground, less where it touched down. A jump counts from its
    /// top, so a jump on the flat falls only its own height. Engine-free.
    /// </summary>
    public sealed class FallTracker
    {
        float? _highest;

        /// <summary>Whether the body is off the ground, as far as the tracker knows.</summary>
        public bool Airborne => _highest.HasValue;

        /// <summary>
        /// Follows the body for a frame, standing on something or not, its feet at <paramref name="height"/>; the meters it
        /// fell when this frame touches down, else 0.
        /// </summary>
        public float Step(bool grounded, float height)
        {
            if (!grounded)
            {
                _highest = Math.Max(_highest ?? height, height);
                return 0f;
            }

            if (!_highest.HasValue)
                return 0f;
            var fell = _highest.Value - height;
            _highest = null;
            return Math.Max(0f, fell);
        }

        /// <summary>The world moved under the body by <paramref name="dy"/> (a rebase); a fall in progress moves with it.</summary>
        public void Shift(float dy)
        {
            if (_highest.HasValue)
                _highest += dy;
        }

        /// <summary>Forgets any fall in progress (the body was put somewhere).</summary>
        public void Reset() => _highest = null;
    }
}
