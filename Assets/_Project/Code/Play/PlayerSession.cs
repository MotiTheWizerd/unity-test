using System;
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

        /// <summary>Stands the player at <paramref name="feet"/> (scene coordinates), facing <paramref name="heading"/>, and gives it the camera and the controls.</summary>
        public void Spawn(Vector3 feet, float heading)
        {
            Despawn();
            _motor = new CharacterControllerMotor(_profile);
            if (_profile.Model != null)
                _avatar = new PlayerAvatar(_motor.Body, _profile.Model);
            Player = new PlayerCharacter(_motor, _profile.Locomotion(), _profile.EyeHeight);
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
            if (Player != null)
                Player.Motor.Teleport(Player.Feet + delta.ToNumerics());
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

        // The camera frames the player as the current view says; the model turns with the body and shows unless the camera is inside it.
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
        }
    }
}
