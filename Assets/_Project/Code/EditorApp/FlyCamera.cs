using System;
using Forge.Editing.Geometry;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Forge.EditorApp
{
    /// <summary>
    /// Editor-style camera, like Unity's and Unreal's scene views:
    /// hold right mouse to look and fly with WASD/QE (Shift = fast); mouse wheel dollies
    /// towards the point under the cursor; hold middle mouse to pan, with the world staying
    /// under the cursor; <see cref="Frame"/> glides to fit a selection.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FlyCamera : MonoBehaviour, ICameraRig
    {
        // Scroll units per wheel notch on Windows; one notch = one zoom step.
        const float ScrollPerNotch = 120f;

        // Depth assumed when nothing is under the cursor (e.g. pointing at the sky).
        const float FallbackDepth = 50f;

        [SerializeField] float _moveSpeed = 20f;
        [SerializeField] float _fastMultiplier = 4f;
        [SerializeField] float _lookSensitivity = 0.15f;

        [SerializeField, Range(0.05f, 0.5f), Tooltip("Fraction of the distance to the point under the cursor covered by one wheel notch.")]
        float _zoomStep = 0.15f;

        [SerializeField, Tooltip("Smallest dolly per wheel notch, in meters, so zooming never stalls.")]
        float _minZoomStep = 0.5f;

        [SerializeField, Tooltip("Seconds a Frame (F) glide takes.")]
        float _frameDuration = 0.25f;

        [SerializeField, Tooltip("Extra room around a framed selection (1 = tight).")]
        float _framePadding = 1.3f;

        [NonSerialized] Camera _camera;
        [NonSerialized] IInputBlocker _ui;
        [NonSerialized] Func<Ray, float?> _depthUnder;
        [NonSerialized] float _yaw;
        [NonSerialized] float _pitch;
        [NonSerialized] float _panDepth;
        [NonSerialized] bool _panning;
        [NonSerialized] Vector3? _glideTarget;
        [NonSerialized] Vector3 _glideVelocity;

        /// <param name="ui">Wheel and pan gestures don't start over this UI.</param>
        /// <param name="depthUnder">Distance along a ray to the first world surface, or null; used to scale zoom and pan.</param>
        public void Initialize(IInputBlocker ui, Func<Ray, float?> depthUnder)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _depthUnder = depthUnder ?? throw new ArgumentNullException(nameof(depthUnder));
        }

        public void Frame(Vector3 center, float radius)
        {
            var distance = ViewMath.FitDistance(Mathf.Max(radius, 0.5f) * _framePadding, _camera.fieldOfView, _camera.aspect);
            _glideTarget = center - transform.forward * distance;
            _glideVelocity = Vector3.zero;
        }

        void Awake() => _camera = GetComponent<Camera>();

        void Start()
        {
            var euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            var pointer = mouse.position.ReadValue();
            var fast = keyboard.shiftKey.isPressed ? _fastMultiplier : 1f;
            var manual = Look(keyboard, mouse, fast) | Zoom(mouse, pointer, fast) | Pan(mouse, pointer);

            // Any manual movement takes over from a Frame glide.
            if (manual)
                _glideTarget = null;
            else
                Glide();
        }

        // Like Unity/Unreal: the camera only owns the keyboard while right mouse is held,
        // leaving keys free for editor shortcuts the rest of the time.
        bool Look(Keyboard keyboard, Mouse mouse, float fast)
        {
            if (!mouse.rightButton.isPressed)
                return false;

            var delta = mouse.delta.ReadValue() * _lookSensitivity;
            _yaw += delta.x;
            _pitch = Mathf.Clamp(_pitch - delta.y, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            var move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += Vector3.forward;
            if (keyboard.sKey.isPressed) move += Vector3.back;
            if (keyboard.aKey.isPressed) move += Vector3.left;
            if (keyboard.dKey.isPressed) move += Vector3.right;
            if (keyboard.eKey.isPressed) move += Vector3.up;
            if (keyboard.qKey.isPressed) move += Vector3.down;

            transform.Translate(move.normalized * _moveSpeed * fast * Time.unscaledDeltaTime, Space.Self);
            return true;
        }

        // Dolly along the cursor ray by a fraction of the distance to what's under it,
        // so zooming is fast from far away and precise up close.
        bool Zoom(Mouse mouse, Vector2 pointer, float fast)
        {
            var notches = mouse.scroll.ReadValue().y / ScrollPerNotch;
            if (Mathf.Approximately(notches, 0f) || IsOverUI(pointer))
                return false;

            var ray = _camera.ScreenPointToRay(pointer);
            var depth = DepthUnder(ray);
            var step = Mathf.Max(_minZoomStep, depth * _zoomStep) * fast * notches;
            // Never dolly through the surface under the cursor.
            if (step > 0f)
                step = Mathf.Min(step, Mathf.Max(0f, depth - _camera.nearClipPlane * 2f));
            transform.position += ray.direction * step;
            return true;
        }

        // Drag the world with the middle button: the point grabbed stays under the cursor,
        // because one pixel of motion covers more meters the deeper that point is.
        bool Pan(Mouse mouse, Vector2 pointer)
        {
            if (mouse.middleButton.wasPressedThisFrame && !IsOverUI(pointer))
            {
                _panning = true;
                _panDepth = DepthUnder(_camera.ScreenPointToRay(pointer));
            }

            if (!mouse.middleButton.isPressed)
                _panning = false;
            if (!_panning)
                return false;

            var metersPerPixel = 2f * _panDepth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            var delta = mouse.delta.ReadValue() * metersPerPixel;
            transform.position -= transform.right * delta.x + transform.up * delta.y;
            return true;
        }

        void Glide()
        {
            if (!_glideTarget.HasValue)
                return;

            transform.position = Vector3.SmoothDamp(transform.position, _glideTarget.Value, ref _glideVelocity, _frameDuration, Mathf.Infinity, Time.unscaledDeltaTime);
            if ((transform.position - _glideTarget.Value).sqrMagnitude < 1e-4f)
                _glideTarget = null;
        }

        float DepthUnder(Ray ray) => _depthUnder?.Invoke(ray) ?? FallbackDepth;

        bool IsOverUI(Vector2 pointer) => _ui != null && _ui.IsPointerOverUI(pointer);
    }
}
