using System;
using Forge.Agent;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp.Agent
{
    /// <summary>
    /// What the agent can see: the editor's camera, rendered into a picture without the panel. It looks as the user
    /// does, or from a pose of its own, taken only for the one render and put back at once, so the user's view never
    /// moves. Positions are world coordinates (the frame turns them into the scene's). Call on the main thread.
    /// </summary>
    public sealed class AgentEyes
    {
        readonly Camera _camera;
        readonly WorldFrame _frame;
        readonly Func<float, float, float?> _groundHeight;

        /// <param name="groundHeight">The terrain's height at a world (x, z), or null where there is none.</param>
        public AgentEyes(Camera camera, WorldFrame frame, Func<float, float, float?> groundHeight)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _groundHeight = groundHeight ?? throw new ArgumentNullException(nameof(groundHeight));
        }

        /// <summary>Where the user's camera stands, in world coordinates.</summary>
        public Vector3 ViewPosition => _frame.ToWorld(_camera.transform.position);

        /// <summary>The compass heading the user's camera faces: 0 is north (+z), 90 east.</summary>
        public float ViewHeading
        {
            get
            {
                var forward = _camera.transform.forward;
                return Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
            }
        }

        /// <summary>How far the user's camera looks down, in degrees (negative: up).</summary>
        public float ViewPitch => -Mathf.Asin(Mathf.Clamp(_camera.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

        /// <summary>The terrain's height at a world (x, z), or null where there is none.</summary>
        public float? GroundHeight(float x, float z) => _groundHeight(x, z);

        /// <summary>What the user sees now.</summary>
        public AgentImage View() => AgentPictures.Snapshot(_camera);

        /// <summary>The view from a world <paramref name="position"/>, facing <paramref name="heading"/> and down by <paramref name="pitch"/> degrees.</summary>
        public AgentImage From(Vector3 position, float heading, float pitch)
        {
            var view = _camera.transform;
            var keptPosition = view.position;
            var keptRotation = view.rotation;
            try
            {
                view.SetPositionAndRotation(_frame.ToScene(position), Quaternion.Euler(pitch, heading, 0f));
                return AgentPictures.Snapshot(_camera);
            }
            finally
            {
                view.SetPositionAndRotation(keptPosition, keptRotation);
            }
        }
    }
}
