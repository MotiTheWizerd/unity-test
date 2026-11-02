using System.Numerics;

namespace Forge.Editing.Placement
{
    /// <summary>The ground under an object's footprint: the direction to tilt its up to, and the ground height at its center.</summary>
    public readonly struct GroundPlane
    {
        public GroundPlane(Vector3 up, float height)
        {
            Up = up;
            Height = height;
        }

        /// <summary>Unit up direction of the fitted (and tilt-capped) ground plane.</summary>
        public Vector3 Up { get; }

        /// <summary>Ground height under the object's center, from the samples (the footprint's longer axis weighs more).</summary>
        public float Height { get; }
    }
}
