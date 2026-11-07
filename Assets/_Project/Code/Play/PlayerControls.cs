using System;
using Forge.Game.Cameras;
using Forge.Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Forge.Play
{
    /// <summary>
    /// Reads the player's action map (the profile's: Move, Look, Sprint, Jump, ToggleWalk, ToggleView, Zoom, Interact,
    /// Inventory, ReadyWeapon, AttackRight, AttackLeft) into <see cref="PlayerCommands"/>, <see cref="ViewCommands"/> and the menu key, the rules' only input. Rebinding, a gamepad or
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
        readonly InputAction _toggleWalk;
        readonly InputAction _toggleView;
        readonly InputAction _zoom;
        readonly InputAction _interact;
        readonly InputAction _inventory;
        readonly InputAction _readyWeapon;
        readonly InputAction _attackRight;
        readonly InputAction _attackLeft;
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
            _toggleWalk = _map.FindAction("ToggleWalk", throwIfNotFound: true);
            _toggleView = _map.FindAction("ToggleView", throwIfNotFound: true);
            _zoom = _map.FindAction("Zoom", throwIfNotFound: true);
            _interact = _map.FindAction("Interact", throwIfNotFound: true);
            _inventory = _map.FindAction("Inventory", throwIfNotFound: true);
            _readyWeapon = _map.FindAction("ReadyWeapon", throwIfNotFound: true);
            _attackRight = _map.FindAction("AttackRight", throwIfNotFound: true);
            _attackLeft = _map.FindAction("AttackLeft", throwIfNotFound: true);
            _lookSensitivity = profile.LookSensitivity;

            // Prompts name a key the way the actions bind it in their first control scheme (the keyboard's).
            var schemes = profile.Controls.controlSchemes;
            var mask = schemes.Count > 0 ? InputBinding.MaskByGroup(schemes[0].bindingGroup) : (InputBinding?)null;
            InteractKey = KeyOf(_interact, mask);
            InventoryKey = KeyOf(_inventory, mask);
        }

        /// <summary>The use key as a prompt shows it ("E").</summary>
        public string InteractKey { get; }

        /// <summary>The inventory's key as a prompt shows it ("I").</summary>
        public string InventoryKey { get; }

        /// <summary>Whether the inventory's key was pressed this frame (open or close it); never while the map is off.</summary>
        public bool ReadInventory() => _map.enabled && _inventory.WasPressedThisFrame();

        /// <summary>Whether the ready-weapon key was pressed this frame (draw or sheathe); never while the map is off.</summary>
        public bool ReadReadyWeapon() => _map.enabled && _readyWeapon.WasPressedThisFrame();

        /// <summary>
        /// Whether the right or left hand's attack key was pressed this frame (Skyrim's: the left button swings the right
        /// hand, the right button the left); never while the map is off.
        /// </summary>
        public bool ReadAttack(bool right) => _map.enabled && (right ? _attackRight : _attackLeft).WasPressedThisFrame();

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
                _jump.WasPressedThisFrame(),
                _toggleWalk.WasPressedThisFrame(),
                activate: _interact.WasPressedThisFrame());
        }

        /// <summary>This frame's view commands; nothing while the map is off.</summary>
        public ViewCommands ReadView() =>
            _map.enabled ? new ViewCommands(_toggleView.WasPressedThisFrame(), _zoom.ReadValue<float>()) : ViewCommands.None;

        public void Dispose() => Disable();

        // An action's first binding, as the keyboard's scheme shows it ("E"; "I" of "I | Tab").
        static string KeyOf(InputAction action, InputBinding? mask)
        {
            var shown = mask.HasValue ? action.GetBindingDisplayString(mask.Value) : action.GetBindingDisplayString();
            var split = shown.IndexOf(" | ", StringComparison.Ordinal);
            return split < 0 ? shown : shown.Substring(0, split);
        }
    }
}
