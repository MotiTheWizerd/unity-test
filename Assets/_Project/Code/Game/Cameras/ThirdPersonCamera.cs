using System;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Player;

namespace Forge.Game.Cameras
{
    /// <summary>
    /// Over the shoulder, as in Skyrim's third person: the camera orbits a pivot at the shoulders (a little to the right)
    /// along the look, <see cref="Distance"/> behind it. Something in the way (a wall, a slope, a tree) pulls it in at once,
    /// so it never looks through; once clear it eases back out. The wheel sets <see cref="Distance"/>, and the camera
    /// glides there (<see cref="ThirdPersonSettings.ZoomEase"/>) rather than jumping. The body turns toward where it goes,
    /// and the look orbits freely while it stands.
    /// </summary>
    public sealed class ThirdPersonCamera : ICameraMode
    {
        readonly ThirdPersonSettings _settings;
        readonly ICameraObstacles _obstacles;
        float _zoomed = float.NaN;
        float _shown = float.NaN;

        /// <param name="obstacles">What the camera can't pass through; null for nothing (it never pulls in).</param>
        public ThirdPersonCamera(ThirdPersonSettings settings = null, ICameraObstacles obstacles = null)
        {
            _settings = settings ?? ThirdPersonSettings.Default;
            _obstacles = obstacles;
            Distance = _settings.Distance;
        }

        public ThirdPersonSettings Settings => _settings;

        public BodyFacing Facing => BodyFacing.Movement;

        public bool InsideHead => false;

        public float? NearClip => null;

        /// <summary>How far behind the pivot the camera wants to be, in meters (the wheel sets it).</summary>
        public float Distance { get; private set; }

        /// <summary>How far behind the pivot it was last framed: as far as the zoom has glided, less anything in the way.</summary>
        public float ShownDistance => float.IsNaN(_shown) ? Distance : _shown;

        /// <summary>Whether the wheel has brought it as close as it comes.</summary>
        public bool IsClosest => Distance <= _settings.MinDistance + 1e-4f;

        /// <summary>Wheel notches: positive brings it nearer, negative takes it farther, within the settings' range.</summary>
        public void Zoom(float notches)
        {
            if (!float.IsNaN(notches) && !float.IsInfinity(notches))
                Distance = _settings.Clamp(Distance - notches * _settings.ZoomStep);
        }

        /// <summary>Starts framing afresh, <paramref name="distance"/> behind (null: where it was), with no easing from before.</summary>
        public void Begin(float? distance = null)
        {
            if (distance.HasValue)
                Distance = _settings.Clamp(distance.Value);
            _zoomed = float.NaN;
            _shown = float.NaN;
        }

        public ViewPose Frame(PlayerCharacter player, float deltaTime)
        {
            var rotation = FirstPersonCamera.Looking(player.Look);
            var back = -Vector3.Transform(Vector3.UnitZ, rotation);
            var shoulderSide = Compass.Right(player.Look.Heading);

            // The shoulder sits beside the head, down with it in a crouch; a wall right beside the player keeps it in.
            var head = player.Feet + new Vector3(0f, _settings.PivotHeight - player.Lowered, 0f);
            var shoulder = head + new Vector3(shoulderSide.X, 0f, shoulderSide.Y) * _settings.ShoulderOffset;
            var pivot = Toward(head, shoulder);

            // The wheel's distance glides in (ZoomEase); walls still pull the camera in at once.
            _zoomed = float.IsNaN(_zoomed) ? Distance : _zoomed + (Distance - _zoomed) * Glide.Share(deltaTime, _settings.ZoomEase);
            var free = Free(pivot, pivot + back * _zoomed, _zoomed);
            _shown = float.IsNaN(_shown) || free < _shown || !(deltaTime > 0f)
                ? free
                : Math.Min(free, _shown + _settings.ReturnSpeed * deltaTime);
            return new ViewPose(pivot + back * _shown, rotation);
        }

        // How far from `from` toward `to` the camera can go, at most `length`.
        float Free(Vector3 from, Vector3 to, float length) =>
            _obstacles == null ? length : Math.Max(0f, Math.Min(length, _obstacles.Clearance(from, to, _settings.Radius)));

        // As far from `from` toward `to` as the camera fits.
        Vector3 Toward(Vector3 from, Vector3 to)
        {
            var length = Vector3.Distance(from, to);
            if (length < 1e-6f)
                return from;
            return from + (to - from) / length * Free(from, to, length);
        }
    }
}
