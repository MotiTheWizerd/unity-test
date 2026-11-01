using UnityEngine;
using UnityEngine.InputSystem;

namespace Forge.EditorApp
{
    /// <summary>Editor-style free camera: hold right mouse to look, WASD/QE to move, Shift to go fast.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FlyCamera : MonoBehaviour
    {
        [SerializeField] float _moveSpeed = 20f;
        [SerializeField] float _fastMultiplier = 4f;
        [SerializeField] float _lookSensitivity = 0.15f;

        float _yaw;
        float _pitch;

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

            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue() * _lookSensitivity;
                _yaw += delta.x;
                _pitch = Mathf.Clamp(_pitch - delta.y, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            var move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += Vector3.forward;
            if (keyboard.sKey.isPressed && !keyboard.ctrlKey.isPressed) move += Vector3.back;
            if (keyboard.aKey.isPressed) move += Vector3.left;
            if (keyboard.dKey.isPressed) move += Vector3.right;
            if (keyboard.eKey.isPressed) move += Vector3.up;
            if (keyboard.qKey.isPressed) move += Vector3.down;

            var speed = _moveSpeed * (keyboard.shiftKey.isPressed ? _fastMultiplier : 1f);
            transform.Translate(move.normalized * speed * Time.unscaledDeltaTime, Space.Self);
        }
    }
}
