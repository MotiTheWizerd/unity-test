using System;

namespace Forge.Game.Cameras
{
    /// <summary>
    /// A camera easing toward where it should be, the same whatever the frame rate: in <c>seconds</c> it covers two thirds
    /// of the way, then two thirds of what is left, and so on.
    /// </summary>
    public static class Glide
    {
        /// <summary>
        /// The share of the way left that is covered in <paramref name="deltaTime"/> seconds, 0 to 1: all of it on a frame
        /// with no time (a start), or when <paramref name="seconds"/> is zero (no easing).
        /// </summary>
        public static float Share(float deltaTime, float seconds) =>
            !(deltaTime > 0f) || !(seconds > 0f) ? 1f : 1f - (float)Math.Exp(-deltaTime / seconds);
    }
}
