using System;
using System.Collections.Generic;
using System.Numerics;

namespace Forge.Editing.Placement
{
    /// <summary>
    /// Lays out a row of objects along a dragged path (engine-free; positions are on the ground as (x, z)).
    /// The first object sits where the stroke started. Each next one is set down only when the cursor has
    /// gone far enough that it won't overlap the last: the two footprints along the drag direction plus the
    /// gap between them. It lands at exactly that distance, so with gap 0 fences meet end to end.
    /// In line mode the row runs straight from the start towards the cursor and is laid out afresh each time.
    /// </summary>
    /// <remarks>
    /// Objects that align to the path (fences, walls) point their length (local +X) along the drag;
    /// others keep their drawn heading. Each object's variation and the gap before it are drawn once per
    /// position in the row, so the preview doesn't flicker while the row is re-laid.
    /// </remarks>
    public sealed class PlacementStroke
    {
        /// <summary>Spacing never drops below this, so zero-size footprints can't produce endless objects.</summary>
        public const float MinSpacing = 0.05f;

        /// <summary>A row never grows past this many objects in one stroke.</summary>
        public const int MaxItems = 500;

        /// <summary>
        /// A stroke only becomes a drag once the cursor is this far from the start (or half the first
        /// object's footprint, if longer). Hand jitter during a click stays below it, so a click places
        /// exactly what the preview showed instead of turning to face a few millimetres of movement.
        /// </summary>
        public const float MinDragDistance = 0.5f;

        // Cursor moves shorter than this don't define a direction.
        const float MinDirectionDistance = 0.01f;

        readonly Vector2 _start;
        readonly Footprint _footprint;
        readonly bool _alignToPath;
        readonly float _yawOffset;
        readonly float _dragDistance;
        bool _dragging;
        readonly Func<PlacementRoll> _drawRoll;
        readonly Func<float> _drawGap;
        readonly List<PlacementRoll> _rolls = new List<PlacementRoll>();
        readonly List<float> _gaps = new List<float> { 0f };
        readonly List<StrokeItem> _free = new List<StrokeItem>();
        readonly List<StrokeItem> _line = new List<StrokeItem>();
        bool _lineMode;

        /// <param name="start">Ground position of the first object.</param>
        /// <param name="alignToPath">Once dragging, point each object's length along the drag instead of using its drawn heading.</param>
        /// <param name="yawOffset">The user's [ / ] rotation. Objects that keep their own heading add it to their drawn turn;
        /// path-following objects use it only until the drag starts (a click), after which the drag sets their heading.</param>
        /// <param name="firstRoll">The first object's variation (the one the preview already showed).</param>
        /// <param name="drawRoll">Draws the variation of each further object.</param>
        /// <param name="drawGap">Draws the gap, in meters, before each further object.</param>
        public PlacementStroke(Vector2 start, Footprint footprint, bool alignToPath, float yawOffset,
            PlacementRoll firstRoll, Func<PlacementRoll> drawRoll, Func<float> drawGap)
        {
            _start = start;
            _footprint = footprint;
            _alignToPath = alignToPath;
            _yawOffset = yawOffset;
            _drawRoll = drawRoll ?? throw new ArgumentNullException(nameof(drawRoll));
            _drawGap = drawGap ?? throw new ArgumentNullException(nameof(drawGap));
            _rolls.Add(firstRoll);
            _free.Add(new StrokeItem(start, YawFor(null, firstRoll), firstRoll.Scale));
            _dragDistance = Math.Max(MinDragDistance, Math.Max(footprint.SizeX, footprint.SizeZ) * firstRoll.Scale * 0.5f);
        }

        /// <summary>Whether the cursor has gone far enough from the start for this to be a drag rather than a click.</summary>
        public bool IsDragging => _dragging;

        /// <summary>The row as it stands: the free path, or the straight line while in line mode.</summary>
        public IReadOnlyList<StrokeItem> Items => _lineMode && _dragging ? _line : _free;

        /// <summary>Follows the cursor. <paramref name="line"/> shows a straight row from the start instead of the free path.</summary>
        public void MoveTo(Vector2 cursor, bool line)
        {
            _lineMode = line;
            // Once a drag, always a drag: coming back near the start doesn't turn it into a click again.
            _dragging |= Vector2.Distance(cursor, _start) >= _dragDistance;
            if (!_dragging)
                return;

            if (line)
                LayLine(cursor);
            else
                Extend(cursor);
        }

        // Free path: keep setting objects down towards the cursor while it is far enough from the last one.
        void Extend(Vector2 cursor)
        {
            while (_free.Count < MaxItems)
            {
                var offset = cursor - _free[_free.Count - 1].Position;
                var distance = offset.Length();
                if (distance < MinDirectionDistance)
                    return;

                var direction = offset / distance;
                // Until the second object exists, the first one turns to face the drag.
                if (_free.Count == 1 && _alignToPath)
                    _free[0] = _free[0].WithYaw(YawFor(direction, _rolls[0]));

                var next = NextAfter(_free[_free.Count - 1], direction, out var spacing);
                if (distance < spacing)
                    return;
                _free.Add(next);
            }
        }

        // Straight row from the start towards the cursor; only objects whose center fits on the segment.
        void LayLine(Vector2 cursor)
        {
            _line.Clear();
            var offset = cursor - _start;
            var length = offset.Length();
            if (length < MinDirectionDistance)
            {
                _line.Add(_free[0]);
                return;
            }

            var direction = offset / length;
            _line.Add(new StrokeItem(_start, YawFor(direction, _rolls[0]), _rolls[0].Scale));
            var travelled = 0f;
            while (_line.Count < MaxItems)
            {
                var next = NextAfter(_line[_line.Count - 1], direction, out var spacing);
                if (travelled + spacing > length)
                    return;
                travelled += spacing;
                _line.Add(new StrokeItem(_start + direction * travelled, next.YawDegrees, next.Scale));
            }
        }

        // The object after `last` in `direction`, and how far its center is from last's center.
        StrokeItem NextAfter(StrokeItem last, Vector2 direction, out float spacing)
        {
            var index = _lineMode ? _line.Count : _free.Count;
            var roll = RollAt(index);
            var yaw = YawFor(direction, roll);
            spacing = Math.Max(MinSpacing,
                _footprint.HalfLengthAlong(direction, last.YawDegrees, last.Scale)
                + GapAt(index)
                + _footprint.HalfLengthAlong(direction, yaw, roll.Scale));
            return new StrokeItem(last.Position + direction * spacing, yaw, roll.Scale);
        }

        // A path-following object takes its heading from the drag alone: adding the user's turn on top
        // would set fences across the row, packed side by side. Everything else keeps its own heading.
        float YawFor(Vector2? direction, PlacementRoll roll) =>
            PlacementMath.NormalizeDegrees(_alignToPath && direction.HasValue
                ? PlacementMath.YawAlong(direction.Value)
                : _yawOffset + roll.YawDegrees);

        PlacementRoll RollAt(int index)
        {
            while (_rolls.Count <= index)
                _rolls.Add(_drawRoll());
            return _rolls[index];
        }

        float GapAt(int index)
        {
            while (_gaps.Count <= index)
                _gaps.Add(Math.Max(0f, _drawGap()));
            return _gaps[index];
        }
    }
}
