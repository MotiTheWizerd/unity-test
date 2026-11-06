using System;
using Forge.Game.Cameras;
using Forge.Game.Player;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Puts the engine's camera where the current camera mode (<see cref="ICameraMode"/>) says, every frame while playing.
    /// Switching between first and third person is setting <see cref="Mode"/>; nothing else moves the camera meanwhile.
    /// </summary>
    public sealed class CameraDirector
    {
        readonly Transform _camera;
        ICameraMode _mode;

        public CameraDirector(Transform camera, ICameraMode mode)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            Mode = mode;
        }

        public ICameraMode Mode
        {
            get => _mode;
            set => _mode = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Frames <paramref name="player"/> for this frame of <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(PlayerCharacter player, float deltaTime)
        {
            var pose = _mode.Frame(player, deltaTime);
            _camera.SetPositionAndRotation(pose.Position.ToUnity(), pose.Rotation.ToUnity());
        }
    }
}
