using System;
using System.Collections.Generic;
using Forge.Editing.Geometry;
using UnityEngine;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>
    /// Shows the user's mark (<see cref="GroundMark"/>) whatever tool is active: a post standing on its center,
    /// and around it a small ring for a spot or the area's outline, laid on the terrain. Redrawn only when the
    /// mark or the world changes.
    /// </summary>
    public sealed class MarkView : IDisposable
    {
        const float PostHeight = 8f;
        const float SpotRadius = 1.5f;
        const int SpotSegments = 32;
        const float EdgeStep = 1f;
        const int MaxStepsPerEdge = 128;

        static readonly Color MarkColor = new Color(1f, 0.3f, 0.85f);

        readonly LineGizmo _outline;
        readonly LineGizmo _post;
        readonly List<Vector3> _points = new List<Vector3>();
        readonly TerrainSurface _surface;
        GroundMark _shownMark;
        object _shownWorld;

        /// <param name="worldRoot">What the mark is drawn under, so its world coordinates hold.</param>
        public MarkView(TerrainSurface surface, Transform worldRoot)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _outline = new LineGizmo("MarkOutline", loop: true, worldRoot);
            _post = new LineGizmo("MarkPost", loop: false, worldRoot);
        }

        /// <param name="mark">The mark to show, or null to show none.</param>
        /// <param name="world">The world version the terrain comes from; a new one redraws the mark on the new ground.</param>
        public void Refresh(GroundMark mark, object world)
        {
            if (ReferenceEquals(mark, _shownMark) && ReferenceEquals(world, _shownWorld))
                return;
            _shownMark = mark;
            _shownWorld = world;

            if (mark == null)
            {
                _outline.Hide();
                _post.Hide();
                return;
            }

            var foot = _surface.Drop(Flat(mark.Center));
            _post.Show(new[] { foot, foot + Vector3.up * PostHeight }, 0.15f, MarkColor);

            _points.Clear();
            if (mark.Area.HasValue)
                AddOutline(mark.Area.Value.Corners());
            else
                AddRing(mark.Center);
            _outline.Show(_points, 0.25f, MarkColor);
        }

        public void Dispose()
        {
            _outline.Dispose();
            _post.Dispose();
        }

        // Each edge is walked in short steps, so the outline follows the ground between the corners.
        void AddOutline(System.Numerics.Vector2[] corners)
        {
            for (var i = 0; i < corners.Length; i++)
            {
                var from = corners[i];
                var to = corners[(i + 1) % corners.Length];
                var steps = Mathf.Clamp(Mathf.CeilToInt(System.Numerics.Vector2.Distance(from, to) / EdgeStep), 1, MaxStepsPerEdge);
                for (var s = 0; s < steps; s++)
                    _points.Add(_surface.Drop(Flat(System.Numerics.Vector2.Lerp(from, to, (float)s / steps))));
            }
        }

        void AddRing(System.Numerics.Vector2 center)
        {
            for (var i = 0; i < SpotSegments; i++)
            {
                var angle = i * Mathf.PI * 2f / SpotSegments;
                _points.Add(_surface.Drop(Flat(center) + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SpotRadius));
            }
        }

        static Vector3 Flat(System.Numerics.Vector2 ground) => new Vector3(ground.X, 0f, ground.Y);
    }
}
