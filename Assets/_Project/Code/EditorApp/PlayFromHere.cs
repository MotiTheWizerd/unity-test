using System;
using Forge.EditorApp.Tools;
using Forge.Editing.Modes;
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
    /// playing (<see cref="EditorContext.Modes"/>), the player (<see cref="PlayerSession"/>) is stood on whatever the
    /// middle of the view shows (else the ground under the camera), facing the way the camera faces, and the fly camera
    /// rests while the player has the camera. Going back to editing takes the player away and wakes the fly camera (the
    /// bootstrap puts it back where editing left it). The streaming follows the camera, and so the player.
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
        readonly PlayerSession _session;

        public PlayFromHere(EditorContext context, ScenePicker picker, WorldFrame frame, Camera camera, FlyCamera flyCamera, PlayerProfile profile)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _flyCamera = flyCamera != null ? flyCamera : throw new ArgumentNullException(nameof(flyCamera));
            _session = new PlayerSession(profile, camera.transform);
            _context.Modes.Changed += OnModeChanged;
        }

        /// <summary>The player while playing, else null.</summary>
        public PlayerCharacter Player => _session.Player;

        /// <summary>The player during play: its body, look, controls and view (first or third person).</summary>
        public PlayerSession Session => _session;

        /// <summary>Plays one frame.</summary>
        public void Tick(float deltaTime) => _session.Tick(deltaTime);

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); the player moves with it.</summary>
        public void Shift(Vector3 delta) => _session.Shift(delta);

        public void Dispose()
        {
            _context.Modes.Changed -= OnModeChanged;
            _session.Dispose();
        }

        void OnModeChanged(AppMode mode)
        {
            if (mode == AppMode.Playing)
            {
                var view = _camera.transform;
                _session.Spawn(SpawnFeet(view), Compass.HeadingOf(new System.Numerics.Vector2(view.forward.x, view.forward.z)));
                _flyCamera.enabled = false;
            }
            else
            {
                _session.Despawn();
                _flyCamera.enabled = true;
            }
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
