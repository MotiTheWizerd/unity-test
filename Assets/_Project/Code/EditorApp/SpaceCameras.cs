using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// Where the camera was in each space, so switching back returns to the same view; a space seen for the
    /// first time opens on a default view of its middle.
    /// </summary>
    public sealed class SpaceCameras
    {
        // Looking down at the origin from the south, as the scene's starting camera does.
        static readonly Quaternion Overview = Quaternion.Euler(32f, 0f, 0f);
        static readonly Vector3 WorldspaceOffset = new Vector3(0f, 35f, -75f);
        static readonly Vector3 InteriorOffset = new Vector3(0f, 8f, -12f);

        readonly Dictionary<RecordId, Pose> _poses = new Dictionary<RecordId, Pose>();

        public void Remember(RecordId space, Transform camera) => _poses[space] = new Pose(camera.position, camera.rotation);

        /// <summary>The camera's last pose in <paramref name="space"/>, or the default view of its middle.</summary>
        public Pose Recall(SpaceRecord space)
        {
            if (_poses.TryGetValue(space.Id, out var pose))
                return pose;

            var offset = space.Kind == SpaceKind.Interior ? InteriorOffset : WorldspaceOffset + Vector3.up * space.BaseHeight;
            return new Pose(offset, Overview);
        }
    }
}
