using System;
using Forge.Game.Cameras;
using Forge.Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Forge.Play
{
    /// <summary>
    /// Reads the player's action map (the profile's: Move, Look, Sprint, Jump, ToggleView, Zoom) into
    /// <see cref="PlayerCommands"/> and <see cref="ViewCommands"/>, the rules' only input. Rebinding, a gamepad or
    /// another layout is the actions asset's business; nothing here names a key (the wheel's binding scales its steps to
    /// notches). The map is on only while enabled (while playing).
    /// </summary>
    public sealed class PlayerControls : IDisposable
    {
        readonly InputActionMap _map;
        readonly InputAction _move;
        readonly InputAction _look;
        readonly InputAction _sprint;
        readonly InputAction _jump;
        readonly InputAction _toggleView;
        readonly InputAction _zoom;
        readonly float _lookSensitivity;

        public PlayerControls(PlayerProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (profile.Controls == null)
                throw new ArgumentException($"Player profile '{profile.name}' has no input actions (PlayerProfile._controls).", nameof(profile));

            // Missing actions fail at once, naming what is missing, rather than leaving a key that silently does nothing.
            _map = profile.Controls.FindActionMap(profile.ControlsMap, throwIfNotFound: true);
            _move = _map.FindAction("Move", throwIfNotFound: true);
            _look = _map.FindAction("Look", throwIfNotFound: true);
            _sprint = _map.FindAction("Sprint", throwIfNotFound: true);
            _jump = _map.FindAction("Jump", throwIfNotFound: true);
            _toggleView = _map.FindAction("ToggleView", throwIfNotFound: true);
            _zoom = _map.FindAction("Zoom", throwIfNotFound: true);
            _lookSensitivity = profile.LookSensitivity;
        }

        public void Enable() => _map.Enable();

        public void Disable() => _map.Disable();

        /// <summary>This frame's commands; nothing while the map is off.</summary>
        public PlayerCommands Read()
        {
            if (!_map.enabled)
                return PlayerCommands.None;

            var move = _move.ReadValue<Vector2>();
            // Pixels the pointer moved this frame, turned into degrees; screen y grows up, as the look's pitch does.
            var look = _look.ReadValue<Vector2>() * _lookSensitivity;
            return new PlayerCommands(
                new System.Numerics.Vector2(move.x, move.y),
                new System.Numerics.Vector2(look.x, look.y),
                _sprint.IsPressed(),
                _jump.WasPressedThisFrame());
        }

        /// <summary>This frame's view commands; nothing while the map is off.</summary>
        public ViewCommands ReadView() =>
            _map.enabled ? new ViewCommands(_toggleView.WasPressedThisFrame(), _zoom.ReadValue<float>()) : ViewCommands.None;

        public void Dispose() => Disable();
    }
}
