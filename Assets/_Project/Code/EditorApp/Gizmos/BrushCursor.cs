using System;
using Forge.Editing.Terrain;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>
    /// Ring drawn on the terrain showing the brush footprint, colored by mode.
    /// Each point is dropped onto the surface so the ring hugs hills.
    /// </summary>
    public sealed class BrushCursor : IDisposable
    {
        const int Segments = 64;
        const float ProbeHeight = 500f;

        readonly LineRenderer _line;
        readonly Material _material;
        readonly Vector3[] _points = new Vector3[Segments];
        readonly Func<Ray, (bool hit, Vector3 point)> _surface;

        /// <param name="surface">Finds the terrain surface along a ray (supplied by the picker).</param>
        public BrushCursor(Func<Ray, (bool hit, Vector3 point)> surface)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));

            var go = new GameObject("BrushCursor");
            go.SetActive(false);
            _line = go.AddComponent<LineRenderer>();
            _line.loop = true;
            _line.positionCount = Segments;
            _line.useWorldSpace = true;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _material = new Material(Shader.Find("Sprites/Default"));
            _line.sharedMaterial = _material;
        }

        public void Show(Vector3 center, float radius, BrushMode mode)
        {
            for (var i = 0; i < Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                var flat = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                var probe = new Ray(flat + Vector3.up * ProbeHeight, Vector3.down);
                var (hit, point) = _surface(probe);
                _points[i] = (hit ? point : flat) + Vector3.up * 0.2f;
            }

            _line.SetPositions(_points);
            _line.widthMultiplier = Mathf.Max(0.1f, radius * 0.03f);
            _line.startColor = _line.endColor = ColorFor(mode);
            _line.gameObject.SetActive(true);
        }

        public void Hide() => _line.gameObject.SetActive(false);

        public void Dispose()
        {
            if (_line != null)
                Object.Destroy(_line.gameObject);
            if (_material != null)
                Object.Destroy(_material);
        }

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
