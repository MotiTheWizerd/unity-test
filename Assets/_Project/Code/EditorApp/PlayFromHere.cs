using System;
using Forge.EditorApp.Tools;
using Forge.Editing.Modes;
using Forge.Game.Cameras;
using Forge.Game.Player;
using Forge.Play;
using Forge.World;
using UnityEngine;
// Not UnityEngine.Compass, the device's magnetometer.
using Compass = Forge.Core.Geometry.Compass;

namespace Forge.EditorApp
{
    /// <summary>
    /// Plays the world from where the editor looks, as the Creation Kit's play-in-place does. When the app starts
    /// playing (<see cref="EditorContext.Modes"/>), the player is stood on whatever the middle of the view shows (else
    /// the ground under the camera), facing the way the camera faces; the fly camera rests and the
    /// <see cref="CameraDirector"/> takes the camera, first person; the mouse is captured and the player's controls
    /// switched on. Going back to editing undoes all of it (the bootstrap puts the camera back where editing left it).
    /// The streaming follows the camera, and so the player.
    /// </summary>
    public sealed class PlayFromHere : IDisposable
    {
        // Feet are set down this far above the surface picked, so the body never starts inside it.
        const float SpawnClearance = 0.05f;

        readonly EditorContext _context;
        readonly ScenePicker _picker;
        readonly WorldFrame _frame;
        readonly Camera _camera;
        readonly FlyCamera _flyCamera;
        readonly PlayerProfile _profile;
        readonly PlayerControls _controls;
        readonly CameraDirector _director;
        CharacterControllerMotor _motor;

        public PlayFromHere(EditorContext context, ScenePicker picker, WorldFrame frame, Camera camera, FlyCamera flyCamera, PlayerProfile profile)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _flyCamera = flyCamera != null ? flyCamera : throw new ArgumentNullException(nameof(flyCamera));
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _controls = new PlayerControls(profile);
            _director = new CameraDirector(camera.transform, new FirstPersonCamera());
            _context.Modes.Changed += OnModeChanged;
        }

        /// <summary>The player while playing, else null.</summary>
        public PlayerCharacter Player { get; private set; }

        /// <summary>Plays one frame: the controls drive the player, the camera follows it.</summary>
        public void Tick(float deltaTime)
        {
            if (Player == null)
                return;
            Player.Tick(_controls.Read(), deltaTime);
            _director.Tick(Player, deltaTime);
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); the player moves with it.</summary>
        public void Shift(Vector3 delta)
        {
            if (Player != null)
                Player.Motor.Teleport(Player.Feet + delta.ToNumerics());
        }

        public void Dispose()
        {
            _context.Modes.Changed -= OnModeChanged;
            End();
            _controls.Dispose();
        }

        void OnModeChanged(AppMode mode)
        {
            if (mode == AppMode.Playing)
                Begin();
            else
                End();
        }

        void Begin()
        {
            End();
            var view = _camera.transform;
            var feet = SpawnFeet(view);
            var heading = Compass.HeadingOf(new System.Numerics.Vector2(view.forward.x, view.forward.z));
            _motor = new CharacterControllerMotor(_profile);
            Player = new PlayerCharacter(_motor, _profile.Locomotion(), _profile.EyeHeight);
            Player.PlaceAt(feet.ToNumerics(), heading);

            _flyCamera.enabled = false;
            _director.Tick(Player, 0f);
            _controls.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void End()
        {
            if (Player == null)
                return;
            _controls.Disable();
            _motor.Dispose();
            _motor = null;
            Player = null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _flyCamera.enabled = true;
        }

        // Where the feet start, in scene coordinates: on what the middle of the view shows, else on the ground under the
        // camera, else at the camera (and the player falls until something stops it).
        Vector3 SpawnFeet(Transform view)
        {
            var middle = _frame.ToWorld(_camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)));
            var below = new Ray(_frame.ToWorld(view.position), Vector3.down);
            if (_picker.TryPickWorld(middle, out var hit) || _picker.TryPickWorld(below, out hit))
                return _frame.ToScene(hit.Point) + Vector3.up * SpawnClearance;
            return view.position;
        }
    }
}
