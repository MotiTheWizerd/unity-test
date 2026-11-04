using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Editing.Geometry;

namespace Forge.Editing.Placement
{
    /// <summary>
    /// Lays one kind of object over a rectangle on the ground (engine-free; positions are (x, z)), the one layout of
    /// the outline tool and the <c>place_outline</c> command.
    /// <list type="bullet">
    /// <item><see cref="RectPattern.Outline"/>: pieces stand end to end just inside the rectangle's edge, in a
    /// pinwheel: each side starts at its own corner and stops one piece thickness short of the next, where the next
    /// side starts. Every corner is closed by exactly one piece, with no notch, no overlap and nothing sticking out.
    /// Each side is the rectangle's length minus one thickness, the same both ways, so a floor of whole tiles dragged
    /// from the same corner meets the walls exactly: under the near walls, up to the far walls' inner face.</item>
    /// <item><see cref="RectPattern.Fill"/>: rows of pieces over all of it.</item>
    /// </list>
    /// A piece's length is its footprint across its front (local +X), its thickness front to back. Pieces are spaced
    /// by their footprint at scale 1 plus the gap. When a side isn't a whole number of pieces long, its last piece
    /// slides back to end exactly at the corner, overlapping its neighbour; <see cref="SnapSize"/> sizes a rectangle
    /// so that never happens.
    /// </summary>
    public static class RectLayout
    {
        /// <summary>Most pieces one rectangle lays; a painted row has the same cap.</summary>
        public const int MaxItems = PlacementStroke.MaxItems;

        /// <summary>The smallest side a dragged rectangle gets, so it is never empty.</summary>
        public const float MinSize = 0.1f;

        // Float error in a span that is a whole number of pieces must not add a piece.
        const float CountTolerance = 1e-4f;

        /// <summary>
        /// The rectangle dragged from <paramref name="start"/> (a corner, which stays put) towards
        /// <paramref name="pointer"/>, turned <paramref name="headingDegrees"/>. <paramref name="square"/> makes both
        /// sides the longer one; <paramref name="snapToPieces"/> sizes it to whole pieces (<see cref="SnapSize"/>).
        /// </summary>
        public static GroundRect FromDrag(Vector2 start, Vector2 pointer, float headingDegrees, bool square,
            RectPattern pattern, Footprint piece, float gap, bool snapToPieces)
        {
            var across = PlacementMath.LocalXAxis(headingDegrees);
            var front = new Vector2(-across.Y, across.X);
            var offset = pointer - start;
            var alongAcross = Vector2.Dot(offset, across);
            var alongFront = Vector2.Dot(offset, front);

            var width = Math.Max(MinSize, Math.Abs(alongAcross));
            var depth = Math.Max(MinSize, Math.Abs(alongFront));
            if (square)
                width = depth = Math.Max(width, depth);
            if (snapToPieces)
                (width, depth) = SnapSize(width, depth, pattern, piece, gap);

            var center = start
                         + across * (Math.Sign(alongAcross) >= 0 ? width : -width) / 2f
                         + front * (Math.Sign(alongFront) >= 0 ? depth : -depth) / 2f;
            return new GroundRect(center, width, depth, headingDegrees);
        }

        /// <summary>
        /// The size nearest (<paramref name="width"/>, <paramref name="depth"/>) that <paramref name="pattern"/> fills
        /// with whole pieces: for an outline, every side a whole number of pieces long (so each way is that plus one
        /// piece thickness, see <see cref="RectLayout"/>); for a fill, whole rows both ways. At least one piece each way.
        /// </summary>
        public static (float Width, float Depth) SnapSize(float width, float depth, RectPattern pattern, Footprint piece, float gap)
        {
            gap = Math.Max(0f, gap);
            if (pattern == RectPattern.Fill)
                return (SnapSpan(width, piece.SizeX, gap), SnapSpan(depth, piece.SizeZ, gap));

            var thickness = piece.SizeZ;
            return (SnapSpan(Math.Max(0f, width - thickness), piece.SizeX, gap) + thickness,
                SnapSpan(Math.Max(0f, depth - thickness), piece.SizeX, gap) + thickness);
        }

        /// <summary>How many pieces <see cref="Lay"/> would lay, without laying them (to check <see cref="MaxItems"/> first).</summary>
        public static long Count(GroundRect rect, RectPattern pattern, Footprint piece, float gap)
        {
            gap = Math.Max(0f, gap);
            if (pattern == RectPattern.Fill)
                return (long)PiecesFor(rect.Width, piece.SizeX, gap) * PiecesFor(rect.Depth, piece.SizeZ, gap);

            return 2L * PiecesFor(rect.Width - piece.SizeZ, piece.SizeX, gap) + 2L * PiecesFor(rect.Depth - piece.SizeZ, piece.SizeX, gap);
        }

        /// <summary>
        /// The pieces of <paramref name="pattern"/> over <paramref name="rect"/>. Pieces that align to it (walls,
        /// tiles) run along their side, or with the rectangle; the rest (trees) keep their drawn heading added to the
        /// rectangle's. Each takes its drawn size.
        /// </summary>
        /// <param name="rollAt">The variation of the piece at an index (stable per index, so a live preview doesn't flicker).</param>
        /// <exception cref="ArgumentException">More than <see cref="MaxItems"/> pieces.</exception>
        public static IReadOnlyList<StrokeItem> Lay(GroundRect rect, RectPattern pattern, Footprint piece, float gap,
            bool alignToRect, Func<int, PlacementRoll> rollAt)
        {
            if (rollAt == null)
                throw new ArgumentNullException(nameof(rollAt));
            gap = Math.Max(0f, gap);
            var count = Count(rect, pattern, piece, gap);
            if (count > MaxItems)
                throw new ArgumentException($"That needs {count} pieces, more than {MaxItems}.", nameof(rect));

            var layout = new Builder(rect, alignToRect, rollAt, (int)count);
            if (pattern == RectPattern.Fill)
                layout.Fill(piece, gap);
            else
                layout.Outline(piece, gap);
            return layout.Items;
        }

        /// <summary>How a pattern relates to its rectangle in words, for undo steps and reports: "around" or "over".</summary>
        public static string Preposition(RectPattern pattern) => pattern == RectPattern.Fill ? "over" : "around";

        static float Step(float size, float gap) => Math.Max(PlacementStroke.MinSpacing, size + gap);

        // Whole pieces of `size` (with `gap` between them) nearest to `span`: at least one.
        static float SnapSpan(float span, float size, float gap)
        {
            var step = Step(size, gap);
            var pieces = Math.Max(1, (int)Math.Round((span + gap) / step));
            return pieces * step - gap;
        }

        // Pieces that cover `span` end to end: enough to reach its end, at least one; none for an empty span.
        static int PiecesFor(float span, float size, float gap)
        {
            if (span <= CountTolerance)
                return 0;
            return Math.Max(1, (int)Math.Ceiling((span + gap) / Step(size, gap) - CountTolerance));
        }

        // Centers along a span, from its start. One piece is centered; otherwise the last slides back to end at the span's end.
        static IEnumerable<float> Centers(float span, float size, float gap)
        {
            var pieces = PiecesFor(span, size, gap);
            if (pieces == 1)
            {
                yield return span / 2f;
                yield break;
            }

            var step = Step(size, gap);
            for (var i = 0; i < pieces; i++)
                yield return Math.Min(size / 2f + i * step, span - size / 2f);
        }

        sealed class Builder
        {
            readonly GroundRect _rect;
            readonly bool _align;
            readonly Func<int, PlacementRoll> _rollAt;

            public Builder(GroundRect rect, bool align, Func<int, PlacementRoll> rollAt, int capacity)
            {
                _rect = rect;
                _align = align;
                _rollAt = rollAt;
                Items = new List<StrokeItem>(capacity);
            }

            public List<StrokeItem> Items { get; }

            // A pinwheel, walked around: back, right, front, left. Each side runs from its corner, along the edge,
            // one thickness inside it, and stops one thickness short of the next corner, which the next side covers.
            public void Outline(Footprint piece, float gap)
            {
                var across = _rect.Across;
                var front = _rect.Front;
                var thickness = piece.SizeZ;
                var inset = thickness / 2f;
                var corners = _rect.Corners(); // back-left, back-right, front-right, front-left
                var widthSpan = _rect.Width - thickness;
                var depthSpan = _rect.Depth - thickness;

                Side(corners[0] + front * inset, across, widthSpan, piece, gap);
                Side(corners[1] - across * inset, front, depthSpan, piece, gap);
                Side(corners[2] - front * inset, -across, widthSpan, piece, gap);
                Side(corners[3] + across * inset, -front, depthSpan, piece, gap);
            }

            public void Fill(Footprint piece, float gap)
            {
                var across = _rect.Across;
                var front = _rect.Front;
                var corner = _rect.Center - across * (_rect.Width / 2f) - front * (_rect.Depth / 2f);
                foreach (var z in Centers(_rect.Depth, piece.SizeZ, gap))
                foreach (var x in Centers(_rect.Width, piece.SizeX, gap))
                    Add(corner + across * x + front * z, across);
            }

            void Side(Vector2 start, Vector2 direction, float span, Footprint piece, float gap)
            {
                foreach (var s in Centers(span, piece.SizeX, gap))
                    Add(start + direction * s, direction);
            }

            void Add(Vector2 position, Vector2 direction)
            {
                var roll = _rollAt(Items.Count);
                var yaw = _align
                    ? PlacementMath.YawAlong(direction)
                    : PlacementMath.NormalizeDegrees(_rect.HeadingDegrees + roll.YawDegrees);
                Items.Add(new StrokeItem(position, yaw, roll.Scale));
            }
        }
    }
}
