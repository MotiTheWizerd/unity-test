using System;
using Forge.Game.Cameras;
using Forge.Game.Player;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Puts the engine's camera where the current camera mode (<see cref="ICameraMode"/>) says, every frame while playing,
    /// and has it see as near as the mode asks (first person: the player's own hands). Switching between first and third
    /// person is setting <see cref="Mode"/>; nothing else moves the camera meanwhile. <see cref="Release"/> gives the
    /// camera back as it was set.
    /// </summary>
    public sealed class CameraDirector
    {
        readonly Transform _camera;
        readonly Camera _lens;
        ICameraMode _mode;
        float? _ownNearClip;

        public CameraDirector(Transform camera, ICameraMode mode)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _lens = camera.GetComponent<Camera>();
            Mode = mode;
        }

        public ICameraMode Mode
        {
            get => _mode;
            set => _mode = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Where the camera was put last (scene coordinates).</summary>
        public ViewPose Pose { get; private set; }

        /// <summary>Frames <paramref name="player"/> for this frame of <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(PlayerCharacter player, float deltaTime)
        {
            Pose = _mode.Frame(player, deltaTime);
            _camera.SetPositionAndRotation(Pose.Position.ToUnity(), Pose.Rotation.ToUnity());
            SeeAsNearAs(_mode.NearClip);
        }

        /// <summary>The camera sees as near as it was set to before a mode asked otherwise.</summary>
        public void Release() => SeeAsNearAs(null);

        void SeeAsNearAs(float? nearClip)
        {
            if (_lens == null)
                return;
            if (nearClip.HasValue)
            {
                _ownNearClip ??= _lens.nearClipPlane;
                _lens.nearClipPlane = nearClip.Value;
            }
            else if (_ownNearClip.HasValue)
            {
                _lens.nearClipPlane = _ownNearClip.Value;
                _ownNearClip = null;
            }
        }
    }
}
