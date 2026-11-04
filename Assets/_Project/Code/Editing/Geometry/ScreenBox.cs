using System.Numerics;

namespace Forge.Editing.Geometry
{
    /// <summary>
    /// A rectangle on the screen spanned by two corners in any order, in pixels: the box a drag draws to select
    /// everything inside it.
    /// </summary>
    public readonly struct ScreenBox
    {
        public ScreenBox(Vector2 corner, Vector2 opposite)
        {
            Min = Vector2.Min(corner, opposite);
            Max = Vector2.Max(corner, opposite);
        }

        /// <summary>The corner with the smallest coordinates.</summary>
        public Vector2 Min { get; }

        /// <summary>The corner with the largest coordinates.</summary>
        public Vector2 Max { get; }

        public float Width => Max.X - Min.X;

        public float Height => Max.Y - Min.Y;

        /// <summary>Whether <paramref name="point"/> lies inside the box or on its edge.</summary>
        public bool Contains(Vector2 point) =>
            point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;
    }
}
