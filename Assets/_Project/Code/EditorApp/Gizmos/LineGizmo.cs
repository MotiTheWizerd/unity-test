using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>A flat-colored line in the world (open or closed), shown and hidden on demand.</summary>
    public sealed class LineGizmo : IDisposable
    {
        readonly LineRenderer _line;
        readonly Material _material;
        Vector3[] _buffer = new Vector3[0];

        public LineGizmo(string name, bool loop)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _line = go.AddComponent<LineRenderer>();
            _line.loop = loop;
            _line.useWorldSpace = true;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _material = new Material(Shader.Find("Sprites/Default"));
            _line.sharedMaterial = _material;
        }

        public void Show(IReadOnlyList<Vector3> points, float width, Color color)
        {
            if (_buffer.Length != points.Count)
                _buffer = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
                _buffer[i] = points[i];

            _line.positionCount = _buffer.Length;
            _line.SetPositions(_buffer);
            _line.widthMultiplier = width;
            _line.startColor = _line.endColor = color;
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
    }
}
