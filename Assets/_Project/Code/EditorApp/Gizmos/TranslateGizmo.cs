using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>
    /// Runtime translate handle: X/Y/Z arrows plus a center sphere for horizontal
    /// moves. Built from primitives on a dedicated layer, kept at a constant
    /// on-screen size. Purely visual; drag logic lives in the tool.
    /// </summary>
    public sealed class TranslateGizmo : IDisposable
    {
        const float ScreenSizeFactor = 0.1f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color HoverColor = new Color(1f, 0.85f, 0.1f);

        readonly GameObject _root;
        readonly Dictionary<GizmoHandle, List<Renderer>> _renderers = new Dictionary<GizmoHandle, List<Renderer>>();
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        GizmoHandle? _highlighted;

        public TranslateGizmo(int layer)
        {
            _root = new GameObject("TranslateGizmo");
            _root.SetActive(false);

            BuildArrow(GizmoHandle.AxisX, Vector3.right, Quaternion.Euler(0f, 0f, -90f), layer);
            BuildArrow(GizmoHandle.AxisY, Vector3.up, Quaternion.identity, layer);
            BuildArrow(GizmoHandle.AxisZ, Vector3.forward, Quaternion.Euler(90f, 0f, 0f), layer);
            AddPart(GizmoHandle.PlaneXZ, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity, Vector3.one * 0.16f, layer);

            ApplyColors();
        }

        public static Vector3 AxisDirection(GizmoHandle handle)
        {
            switch (handle)
            {
                case GizmoHandle.AxisX: return Vector3.right;
                case GizmoHandle.AxisY: return Vector3.up;
                case GizmoHandle.AxisZ: return Vector3.forward;
                default: return Vector3.zero;
            }
        }

        public void Show(Vector3 pivot, Camera camera)
        {
            _root.SetActive(true);
            _root.transform.position = pivot;
            var distance = Vector3.Distance(camera.transform.position, pivot);
            _root.transform.localScale = Vector3.one * Mathf.Max(0.01f, distance * ScreenSizeFactor);
        }

        public void Hide() => _root.SetActive(false);

        public void Highlight(GizmoHandle? handle)
        {
            if (_highlighted == handle)
                return;
            _highlighted = handle;
            ApplyColors();
        }

        public void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        void BuildArrow(GizmoHandle handle, Vector3 axis, Quaternion rotation, int layer)
        {
            AddPart(handle, PrimitiveType.Cylinder, axis * 0.5f, rotation, new Vector3(0.08f, 0.5f, 0.08f), layer);
            AddPart(handle, PrimitiveType.Cube, axis * 1.05f, rotation, Vector3.one * 0.2f, layer);
        }

        void AddPart(GizmoHandle handle, PrimitiveType type, Vector3 position, Quaternion rotation, Vector3 scale, int layer)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = handle.ToString();
            if (layer >= 0)
                part.layer = layer;
            part.transform.SetParent(_root.transform, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.AddComponent<GizmoHandlePart>().Handle = handle;

            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (!_renderers.TryGetValue(handle, out var list))
                _renderers[handle] = list = new List<Renderer>();
            list.Add(renderer);
        }

        void ApplyColors()
        {
            foreach (var pair in _renderers)
            {
                var color = pair.Key == _highlighted ? HoverColor : BaseColor(pair.Key);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(ColorId, color);
                foreach (var renderer in pair.Value)
                    renderer.SetPropertyBlock(_block);
            }
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
