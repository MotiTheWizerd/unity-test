using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Game.Attributes;
using Forge.Game.Cameras;
using Forge.Game.Player;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The player while the world is played, all built from one <see cref="PlayerProfile"/>: its body
    /// (<see cref="CharacterControllerMotor"/>), its look (<see cref="PlayerAvatar"/>), its controls, its rules
    /// (<see cref="PlayerCharacter"/>) and its camera (first or third person, <see cref="ViewSwitch"/>, put in place by the
    /// <see cref="CameraDirector"/>). <see cref="Spawn"/> stands it somewhere, takes the camera and captures the mouse;
    /// <see cref="Despawn"/> takes it all away again. It lives as long as the app, so the view chosen carries over to
    /// the next spawn. Whatever plays the world uses it: the editor's play from here today, the game itself later.
    /// </summary>
    public sealed class PlayerSession : IDisposable
    {
        readonly PlayerProfile _profile;
        readonly PlayerControls _controls;
        readonly CameraDirector _director;
        CharacterControllerMotor _motor;
        PlayerAvatar _avatar;
        AttributeRecord[] _attributeRecords;

        /// <param name="camera">The camera the player sees through while spawned.</param>
        public PlayerSession(PlayerProfile profile, Transform camera)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _controls = new PlayerControls(profile);
            Views = new ViewSwitch(
                new FirstPersonCamera(),
                new ThirdPersonCamera(profile.ThirdPerson(), new PhysicsCameraObstacles()),
                profile.StartInThirdPerson);
            _director = new CameraDirector(camera, Views.Current);
        }

        /// <summary>First or third person; kept between spawns.</summary>
        public ViewSwitch Views { get; }

        /// <summary>The player while spawned, else null.</summary>
        public PlayerCharacter Player { get; private set; }

        /// <summary>
        /// The player's attributes (health, stamina, ...), kept from one spawn to the next, as a game keeps them; null
        /// until a spawn gives the world's attributes.
        /// </summary>
        public ActorAttributes Attributes { get; private set; }

        /// <summary>
        /// Stands the player at <paramref name="feet"/> (scene coordinates), facing <paramref name="heading"/>, and gives it
        /// the camera and the controls. <paramref name="attributes"/> are the world's (its resolved attribute records):
        /// the player keeps its numbers while they stay the same, and starts afresh from them when they changed.
        /// </summary>
        public void Spawn(Vector3 feet, float heading, IEnumerable<AttributeRecord> attributes)
        {
            Despawn();
            UpdateAttributes(attributes);
            _motor = new CharacterControllerMotor(_profile);
            if (_profile.Model != null)
                _avatar = new PlayerAvatar(_motor.Body, _profile.Model);
            Player = new PlayerCharacter(_motor, _profile.Locomotion(), _profile.EyeHeight, Attributes);
            Player.PlaceAt(feet.ToNumerics(), heading);
            Views.ThirdPerson.Begin();
            Frame(0f);

            _controls.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Plays one frame: the view and the player take their commands, then the camera and the model follow.</summary>
        public void Tick(float deltaTime)
        {
            if (Player == null)
                return;
            Views.Apply(_controls.ReadView());
            Player.Facing = Views.Current.Facing;
            Player.Tick(_controls.Read(), deltaTime);
            Frame(deltaTime);
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); the player moves with it.</summary>
        public void Shift(Vector3 delta)
        {
            Player?.Shift(delta.ToNumerics());
        }

        // Records are immutable, so the same records (by reference) are the same attributes.
        void UpdateAttributes(IEnumerable<AttributeRecord> attributes)
        {
            var records = (attributes ?? throw new ArgumentNullException(nameof(attributes))).ToArray();
            if (_attributeRecords != null && records.Length == _attributeRecords.Length && !records.Except(_attributeRecords).Any())
                return;
            _attributeRecords = records;
            // A world without attributes (one made before them) plays as before: moving costs nothing.
            Attributes = records.Length > 0 ? new ActorAttributes(AttributeSet.From(records)) : null;
        }

        /// <summary>Takes the player away and gives the mouse back; the camera is the caller's again.</summary>
        public void Despawn()
        {
            if (Player == null)
                return;
            _controls.Disable();
            _avatar?.Dispose();
            _avatar = null;
            _motor.Dispose();
            _motor = null;
            Player = null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Dispose()
        {
            Despawn();
            _controls.Dispose();
        }

        // The camera frames the player as the current view says; the model turns with the body, shows unless the camera is inside it, and animates as the body moved.
        void Frame(float deltaTime)
        {
            var view = Views.Current;
            Player.Facing = view.Facing;
            _director.Mode = view;
            _director.Tick(Player, deltaTime);
            if (_avatar == null)
                return;
            _avatar.Face(Player.BodyHeading);
            _avatar.Show(view.ShowsBody);
            if (deltaTime > 0f)
                _avatar.Animate(Player.Motion, deltaTime);
        }
    }
}
