using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>
    /// A runtime gizmo: parts built from primitives on the gizmo layer, each tagged
    /// with the handle it belongs to, tinted on hover and kept at a constant on-screen
    /// size. Purely visual. <see cref="GizmoShapes"/> describes the move, rotate and
    /// scale shapes, and the drag logic lives in the tool.
    /// </summary>
    public sealed class Gizmo : IDisposable
    {
        const float ScreenSizeFactor = 0.1f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color HoverColor = new Color(1f, 0.85f, 0.1f);

        readonly GameObject _root;
        readonly int _layer;
        readonly Material _material;
        readonly Dictionary<GizmoHandle, List<Renderer>> _renderers = new Dictionary<GizmoHandle, List<Renderer>>();
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        GizmoHandle? _highlighted;

        /// <param name="material">
        /// Material for every part, normally the always-on-top GizmoOverlay. Null keeps
        /// the primitives' default material, which the world can hide.
        /// </param>
        /// <param name="worldRoot">What the gizmo sits under, so the world coordinates it is shown at hold.</param>
        public Gizmo(string name, int layer, Material material, Transform worldRoot)
        {
            _root = new GameObject(name);
            _root.transform.SetParent(worldRoot, false);
            _root.SetActive(false);
            _layer = layer;
            _material = material;
        }

        /// <param name="pivot">World coordinates.</param>
        public void Show(Vector3 pivot, Camera camera)
        {
            _root.SetActive(true);
            _root.transform.localPosition = pivot;
            var distance = Vector3.Distance(camera.transform.position, _root.transform.position);
            _root.transform.localScale = Vector3.one * Mathf.Max(0.01f, distance * ScreenSizeFactor);
        }

        public void Hide() => _root.SetActive(false);

        public void Highlight(GizmoHandle? handle)
        {
            if (_highlighted == handle)
                return;
            _highlighted = handle;
            foreach (var pair in _renderers)
                Tint(pair.Key, pair.Value);
        }

        public void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        /// <summary>Adds a primitive in gizmo space (1 unit = the gizmo's on-screen size).</summary>
        public void AddPart(GizmoHandle handle, PrimitiveType type, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = handle.ToString();
            if (_layer >= 0)
                part.layer = _layer;
            part.transform.SetParent(_root.transform, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.AddComponent<GizmoHandlePart>().Handle = handle;

            var renderer = part.GetComponent<Renderer>();
            if (_material != null)
                renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (!_renderers.TryGetValue(handle, out var list))
                _renderers[handle] = list = new List<Renderer>();
            list.Add(renderer);
            Tint(handle, new[] { renderer });
        }

        void Tint(GizmoHandle handle, IEnumerable<Renderer> renderers)
        {
            var color = handle == _highlighted ? HoverColor : BaseColor(handle);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            foreach (var renderer in renderers)
                renderer.SetPropertyBlock(_block);
        }

        static Color BaseColor(GizmoHandle handle)
        {
            switch (handle)
            {
                case GizmoHandle.AxisX: return new Color(0.9f, 0.2f, 0.2f);
                case GizmoHandle.AxisY: return new Color(0.2f, 0.85f, 0.25f);
                case GizmoHandle.AxisZ: return new Color(0.25f, 0.45f, 1f);
                default: return Color.white;
            }
        }
    }
}
