using System;
using Forge.Editing.Terrain;
using UnityEngine;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>
    /// Ring drawn on the terrain showing the brush footprint, colored by mode.
    /// Each point is dropped onto the surface so the ring hugs hills.
    /// </summary>
    public sealed class BrushCursor : IDisposable
    {
        const int Segments = 64;

        readonly LineGizmo _line = new LineGizmo("BrushCursor", loop: true);
        readonly Vector3[] _points = new Vector3[Segments];
        readonly TerrainSurface _surface;

        public BrushCursor(TerrainSurface surface)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        }

        public void Show(Vector3 center, float radius, BrushMode mode)
        {
            for (var i = 0; i < Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                _points[i] = _surface.Drop(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }

            _line.Show(_points, Mathf.Max(0.1f, radius * 0.03f), ColorFor(mode));
        }

        public void Hide() => _line.Hide();

        public void Dispose() => _line.Dispose();

        static Color ColorFor(BrushMode mode)
        {
            switch (mode)
            {
                case BrushMode.Raise: return new Color(0.3f, 1f, 0.4f);
                case BrushMode.Lower: return new Color(1f, 0.35f, 0.3f);
                case BrushMode.Flatten: return new Color(0.35f, 0.6f, 1f);
                default: return Color.white;
            }
        }
    }
}
