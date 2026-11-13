using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// A view of something staged in the editor's scene drawn inside an editor window, so it is seen where it is tuned
    /// rather than in a Scene view that may be hidden behind the Game tab: a camera of its own, never saved, rendered on
    /// demand into a texture the window draws. Drag to turn round it, scroll to come closer.
    /// </summary>
    sealed class StagePreview : IDisposable
    {
        const float MinDistance = 0.5f;
        const float MaxDistance = 8f;

        readonly Camera _camera;
        RenderTexture _texture;
        float _yaw = 200f;
        float _pitch = 10f;
        float _distance = 2.6f;

        public StagePreview()
        {
            var holder = new GameObject("Stage Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            _camera = holder.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.32f, 0.36f, 0.42f);
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
        }

        /// <summary>
        /// Draws what stands round <paramref name="target"/> into <paramref name="area"/>, turned and drawn closer by the
        /// mouse there; true when the mouse moved it, so the window repaints.
        /// </summary>
        public bool Draw(Rect area, Vector3 target)
        {
            if (Steer(area))
                return true;
            if (Event.current.type != EventType.Repaint || area.width < 8f || area.height < 8f)
                return false;

            var width = Mathf.RoundToInt(area.width * EditorGUIUtility.pixelsPerPoint);
            var height = Mathf.RoundToInt(area.height * EditorGUIUtility.pixelsPerPoint);
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                Release();
                _texture = new RenderTexture(width, height, 24) { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 4 };
            }

            var turn = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.SetPositionAndRotation(target - turn * Vector3.forward * _distance, turn);
            _camera.aspect = area.width / area.height;
            _camera.targetTexture = _texture;
            _camera.Render();
            _camera.targetTexture = null;
            GUI.DrawTexture(area, _texture, ScaleMode.StretchToFill, false);
            return false;
        }

        // Dragging turns round the target, the wheel comes closer or backs off; true when it did.
        bool Steer(Rect area)
        {
            var current = Event.current;
            if (!area.Contains(current.mousePosition))
                return false;
            if (current.type == EventType.MouseDrag)
            {
                _yaw += current.delta.x * 0.5f;
                _pitch = Mathf.Clamp(_pitch + current.delta.y * 0.5f, -80f, 80f);
            }
            else if (current.type == EventType.ScrollWheel)
                _distance = Mathf.Clamp(_distance * (1f + current.delta.y * 0.05f), MinDistance, MaxDistance);
            else
                return false;

            current.Use();
            return true;
        }

        void Release()
        {
            if (_texture == null)
                return;
            _texture.Release();
            Object.DestroyImmediate(_texture);
            _texture = null;
        }

        public void Dispose()
        {
            Release();
            if (_camera != null)
                Object.DestroyImmediate(_camera.gameObject);
        }
    }
}
