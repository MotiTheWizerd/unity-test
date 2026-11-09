using System;

namespace Forge.Game.Cameras
{
    /// <summary>
    /// The view closing in a little as a bow is drawn, as Skyrim's does: from none with the arrow just on the string to
    /// <see cref="FullDraw"/> times nearer at full draw, eased (<see cref="Glide"/>) so it neither jumps in nor snaps back
    /// when the arrow is loosed. Engine-free; the game ticks it with how far the bow is drawn and the camera sees through
    /// <see cref="FieldOfView"/>.
    /// </summary>
    public sealed class DrawZoom
    {
        // Near enough to rest: the camera sees as it was set to again, exactly.
        const float Rest = 1e-3f;

        /// <param name="fullDraw">How many times nearer the view is at full draw (1: no zoom).</param>
        /// <param name="ease">Seconds the zoom takes to come most of the way (0: it follows the draw at once).</param>
        public DrawZoom(float fullDraw, float ease)
        {
            if (!(fullDraw >= 1f) || float.IsInfinity(fullDraw))
                throw new ArgumentOutOfRangeException(nameof(fullDraw), fullDraw, "A zoom brings the view nearer, never farther.");
            if (!(ease >= 0f) || float.IsInfinity(ease))
                throw new ArgumentOutOfRangeException(nameof(ease), ease, "Seconds, 0 or more.");
            FullDraw = fullDraw;
            Ease = ease;
        }

        public float FullDraw { get; }

        public float Ease { get; }

        /// <summary>How many times nearer the view is now; 1 with no bow drawn.</summary>
        public float Magnification { get; private set; } = 1f;

        /// <summary>Follows a bow drawn <paramref name="pull"/> of the way (0 to 1) for <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float pull, float deltaTime)
        {
            var target = 1f + (FullDraw - 1f) * Clamp01(pull);
            Magnification += (target - Magnification) * Glide.Share(deltaTime, Ease);
            if (Math.Abs(target - Magnification) < Rest)
                Magnification = target;
        }

        /// <summary>The view stops zooming at once (the player taken away).</summary>
        public void Reset() => Magnification = 1f;

        /// <summary>
        /// The vertical field of view, degrees, that shows <paramref name="magnification"/> times nearer than
        /// <paramref name="fieldOfView"/> does: the view's half-height narrowed by it, not its angle.
        /// </summary>
        public static float FieldOfView(float fieldOfView, float magnification)
        {
            if (!(magnification > 1f))
                return fieldOfView;
            var halfHeight = Math.Tan(fieldOfView * 0.5 * Math.PI / 180.0) / magnification;
            return (float)(2.0 * Math.Atan(halfHeight) * 180.0 / Math.PI);
        }

        static float Clamp01(float value) => value > 0f ? (value < 1f ? value : 1f) : 0f;
    }
}
