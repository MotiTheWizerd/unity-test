using System;
using System.Numerics;

namespace Forge.Editing.Geometry
{
    /// <summary>
    /// A spot or an area the user marks on the ground to show the agent where they mean ("build it here").
    /// Editor state, not world data: it is never saved and never undone. An area runs along the compass axes.
    /// </summary>
    public sealed class GroundMark
    {
        /// <summary>A drag shorter than this on both axes marks a spot, not an area.</summary>
        public const float MinAreaSize = 1f;

        GroundMark(Vector2 center, GroundRect? area)
        {
            Center = center;
            Area = area;
        }

        /// <summary>The spot, or the middle of the area.</summary>
        public Vector2 Center { get; }

        /// <summary>The marked area; null for a spot.</summary>
        public GroundRect? Area { get; }

        public bool IsArea => Area.HasValue;

        public static GroundMark Spot(Vector2 at) => new GroundMark(at, null);

        /// <summary>
        /// What a click or drag from <paramref name="start"/> to <paramref name="end"/> marks: a spot where the
        /// pointer went down if it barely moved, otherwise the area between the two corners (at least
        /// <see cref="MinAreaSize"/> wide each way).
        /// </summary>
        public static GroundMark FromDrag(Vector2 start, Vector2 end)
        {
            var width = Math.Abs(end.X - start.X);
            var depth = Math.Abs(end.Y - start.Y);
            if (width < MinAreaSize && depth < MinAreaSize)
                return Spot(start);

            var center = (start + end) / 2f;
            return new GroundMark(center, new GroundRect(center, Math.Max(width, MinAreaSize), Math.Max(depth, MinAreaSize)));
        }

        /// <summary>Words for the mark, shared by the editor and the agent, e.g. "a 40 × 25 m area from x=70 to 110, z=50 to 75".</summary>
        public string Describe()
        {
            if (!Area.HasValue)
                return FormattableString.Invariant($"a spot at x={Center.X:0.#}, z={Center.Y:0.#}");

            var area = Area.Value;
            var halfWidth = area.Width / 2f;
            var halfDepth = area.Depth / 2f;
            return FormattableString.Invariant(
                $"a {area.Width:0.#} × {area.Depth:0.#} m area from x={Center.X - halfWidth:0.#} to {Center.X + halfWidth:0.#}, z={Center.Y - halfDepth:0.#} to {Center.Y + halfDepth:0.#}");
        }
    }
}
