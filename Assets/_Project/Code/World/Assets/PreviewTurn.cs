using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// How a thing shown in a preview is turned by hand, as Skyrim's inventory lets you turn an item every way: a drag
    /// across spins it around the up axis, a drag up or down tilts it toward or away from the viewer, whichever way it
    /// has been spun, and it turns slowly by itself while nobody holds it. Its pose is a spin around the up axis over a
    /// tilt (<see cref="Rotation"/> = spin × <see cref="Tilt"/>), so spinning never changes how tall or wide it stands
    /// and the framing only moves when it is tilted. The viewer looks along +Z, as <see cref="TurntableFraming"/>'s
    /// camera does. Plain state: whatever shows a thing (an item, a character) keeps one and feeds it the drags.
    /// </summary>
    public sealed class PreviewTurn
    {
        /// <summary>The spin a new subject starts at: three-quarters on, its front (+Z) to the viewer's left.</summary>
        public const float StartSpin = 210f;

        /// <summary>Degrees a second it spins by itself while not held.</summary>
        public const float AutoSpeed = 25f;

        // Each wheel notch zooms by this factor.
        const float ZoomStep = 1.15f;

        public PreviewTurn() => Reset();

        /// <summary>Degrees spun around the up axis.</summary>
        public float Spin { get; private set; }

        /// <summary>The tilt under the spin: how it leans, in its own unspun frame.</summary>
        public Quaternion Tilt { get; private set; }

        /// <summary>How near the view is, within <see cref="TurntableFraming.MinZoom"/> and <see cref="TurntableFraming.MaxZoom"/>.</summary>
        public float Zoom { get; private set; }

        /// <summary>Held by hand: it doesn't turn by itself.</summary>
        public bool Held { get; set; }

        /// <summary>Its pose: the tilt, then the spin around the up axis.</summary>
        public Quaternion Rotation => Quaternion.AngleAxis(Spin, Vector3.up) * Tilt;

        /// <summary>
        /// Turns it as a drag of <paramref name="across"/> and <paramref name="up"/> degrees would: across (right
        /// positive) spins its near side the same way; up (positive) tilts its near side up, its top away.
        /// </summary>
        public void Drag(float across, float up)
        {
            Spin = Mathf.Repeat(Spin - across, 360f);
            if (up == 0f)
                return;
            // About the viewer's right axis, which in the unspun frame is the right axis turned back by the spin.
            var right = Quaternion.AngleAxis(-Spin, Vector3.up) * Vector3.right;
            Tilt = Quaternion.AngleAxis(up, right) * Tilt;
            Tilt = Quaternion.Normalize(Tilt);
        }

        /// <summary>Zooms in by <paramref name="notches"/> (out when negative).</summary>
        public void ZoomBy(float notches) =>
            Zoom = Mathf.Clamp(Zoom * Mathf.Pow(ZoomStep, notches), TurntableFraming.MinZoom, TurntableFraming.MaxZoom);

        /// <summary>Lets <paramref name="deltaTime"/> seconds pass: it spins by itself unless held.</summary>
        public void Tick(float deltaTime)
        {
            if (!Held && deltaTime > 0f)
                Spin = Mathf.Repeat(Spin + AutoSpeed * deltaTime, 360f);
        }

        /// <summary>Back to how a new subject starts: upright, three-quarters on, unzoomed.</summary>
        public void Reset()
        {
            Spin = StartSpin;
            Tilt = Quaternion.identity;
            Zoom = 1f;
        }
    }
}
