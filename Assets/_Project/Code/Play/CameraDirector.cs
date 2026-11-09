using System;
using Forge.Game.Cameras;
using Forge.Game.Player;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Puts the engine's camera where the current camera mode (<see cref="ICameraMode"/>) says, every frame while playing,
    /// and has it see as near as the mode asks (first person: the player's own hands) and as magnified as
    /// <see cref="Magnification"/> says (a drawn bow's zoom). Switching between first and third person is setting
    /// <see cref="Mode"/>; nothing else moves the camera meanwhile. <see cref="Release"/> gives the camera back as it was set.
    /// </summary>
    public sealed class CameraDirector
    {
        readonly Transform _camera;
        readonly Camera _lens;
        ICameraMode _mode;
        float? _ownNearClip;
        float? _ownFieldOfView;

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

        /// <summary>How many times nearer the camera sees than it was set to (<see cref="DrawZoom"/>); 1: as set.</summary>
        public float Magnification { get; set; } = 1f;

        /// <summary>Frames <paramref name="player"/> for this frame of <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(PlayerCharacter player, float deltaTime)
        {
            Pose = _mode.Frame(player, deltaTime);
            _camera.SetPositionAndRotation(Pose.Position.ToUnity(), Pose.Rotation.ToUnity());
            SeeAsNearAs(_mode.NearClip);
            Magnify(Magnification);
        }

        /// <summary>The camera sees as near and as wide as it was set to before a mode or a zoom asked otherwise.</summary>
        public void Release()
        {
            SeeAsNearAs(null);
            Magnification = 1f;
            Magnify(1f);
        }

        // Its own field of view is kept while magnified, and given back once not.
        void Magnify(float magnification)
        {
            if (_lens == null)
                return;
            if (magnification > 1f)
            {
                _ownFieldOfView ??= _lens.fieldOfView;
                _lens.fieldOfView = DrawZoom.FieldOfView(_ownFieldOfView.Value, magnification);
            }
            else if (_ownFieldOfView.HasValue)
            {
                _lens.fieldOfView = _ownFieldOfView.Value;
                _ownFieldOfView = null;
            }
        }

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
