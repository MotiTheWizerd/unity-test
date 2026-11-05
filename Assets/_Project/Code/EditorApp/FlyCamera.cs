using System;
using Forge.Editing.Geometry;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Forge.EditorApp
{
    /// <summary>
    /// Editor-style camera, like Unity's and Unreal's scene views:
    /// hold right mouse to look and fly with WASD/QE (Shift = fast), at the shared <see cref="FlySpeed"/>, which the
    /// mouse wheel steps while flying; otherwise the wheel dollies
    /// towards the point under the cursor; hold middle mouse to pan, with the world staying
    /// under the cursor (<see cref="BeginPan"/> / <see cref="Pan"/>, which the hand tool uses too);
    /// <see cref="Frame"/> glides to fit a selection.
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
        [NonSerialized] FlySpeed _speed = new FlySpeed();
        [NonSerialized] float? _groundY;
        [NonSerialized] float _yaw;
        [NonSerialized] float _pitch;
        [NonSerialized] float _panDepth;
        [NonSerialized] bool _panning;
        [NonSerialized] Vector3? _glideTarget;
        [NonSerialized] Vector3 _glideVelocity;

        /// <param name="ui">Wheel and pan gestures don't start over this UI.</param>
        /// <param name="depthUnder">Distance along a ray to the first world surface, or null; used to scale zoom and pan, and to measure the height above the ground.</param>
        /// <param name="speed">How fast to fly; shared with the UI that shows and picks it.</param>
        public void Initialize(IInputBlocker ui, Func<Ray, float?> depthUnder, FlySpeed speed)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _depthUnder = depthUnder ?? throw new ArgumentNullException(nameof(depthUnder));
            _speed = speed ?? throw new ArgumentNullException(nameof(speed));
        }

        public void Frame(Vector3 center, float radius)
        {
            var distance = ViewMath.FitDistance(Mathf.Max(radius, 0.5f) * _framePadding, _camera.fieldOfView, _camera.aspect);
            _glideTarget = center - transform.forward * distance;
            _glideVelocity = Vector3.zero;
        }

        /// <summary>Puts the camera at <paramref name="position"/> looking along <paramref name="rotation"/> at once, ending any glide.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _glideTarget = null;
            _glideVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);
            SyncAngles();
        }

        /// <summary>Moves the camera by <paramref name="delta"/>, and any glide it is on with it: the scene was rebased under it.</summary>
        public void Shift(Vector3 delta)
        {
            transform.position += delta;
            if (_glideTarget.HasValue)
                _glideTarget += delta;
        }

        void Awake() => _camera = GetComponent<Camera>();

        void Start() => SyncAngles();

        // Looking around adds to these, so they must match the camera's actual rotation.
        void SyncAngles()
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
            // While flying, the wheel picks the fly speed instead of zooming (as in Unity's and Unreal's scene views).
            var flying = mouse.rightButton.isPressed;
            var manual = Look(keyboard, mouse, fast) | (flying ? StepSpeed(mouse) : Zoom(mouse, pointer, fast)) | PanWithMiddleButton(mouse, pointer);

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

            if (move != Vector3.zero)
                transform.Translate(move.normalized * _moveSpeed * _speed.FactorAt(HeightAboveGround()) * fast * Time.unscaledDeltaTime, Space.Self);
            return true;
        }

        // One wheel notch is one speed step; a fast wheel's several notches in a frame step several at once.
        bool StepSpeed(Mouse mouse)
        {
            var notches = mouse.scroll.ReadValue().y / ScrollPerNotch;
            if (Mathf.Approximately(notches, 0f))
                return false;

            _speed.Step(notches > 0f ? Mathf.Max(1, Mathf.RoundToInt(notches)) : Mathf.Min(-1, Mathf.RoundToInt(notches)));
            return true;
        }

        // Meters from the camera down to the ground, measured only when the speed follows the height. Where no ground
        // is under the camera (past the land's edge, or over far land, which has no collider), the last ground
        // height seen stands in, so the speed doesn't jump when the ground below runs out.
        float? HeightAboveGround()
        {
            if (!_speed.FollowsHeight)
                return null;

            var position = transform.position;
            if (DepthBelow(position) is float depth)
                _groundY = position.y - depth;
            return _groundY.HasValue ? Mathf.Max(0f, position.y - _groundY.Value) : (float?)null;
        }

        float? DepthBelow(Vector3 position) => _depthUnder?.Invoke(new Ray(position, Vector3.down));

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

        public void BeginPan(Vector2 screenPoint) => _panDepth = DepthUnder(_camera.ScreenPointToRay(screenPoint));

        // The point grabbed stays under the cursor, because one pixel of motion covers more meters the deeper
        // that point is. Panning takes over from a Frame glide.
        public void Pan(Vector2 screenDelta)
        {
            var metersPerPixel = 2f * _panDepth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            var delta = screenDelta * metersPerPixel;
            transform.position -= transform.right * delta.x + transform.up * delta.y;
            _glideTarget = null;
        }

        // Drag the world with the middle button, in any tool (the hand tool does the same with the left one).
        bool PanWithMiddleButton(Mouse mouse, Vector2 pointer)
        {
            if (mouse.middleButton.wasPressedThisFrame && !IsOverUI(pointer))
            {
                _panning = true;
                BeginPan(pointer);
            }

            if (!mouse.middleButton.isPressed)
                _panning = false;
            if (!_panning)
                return false;

            Pan(mouse.delta.ReadValue());
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
