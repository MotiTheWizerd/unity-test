using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// Where the camera was in each space, in world coordinates, so switching back returns to the same view; a space
    /// seen for the first time opens on its player start, else on a default view of its middle.
    /// </summary>
    public sealed class SpaceCameras
    {
        // Looking down at the origin from the south, as the scene's starting camera does.
        static readonly Quaternion Overview = Quaternion.Euler(32f, 0f, 0f);
        static readonly Vector3 WorldspaceOffset = new Vector3(0f, 35f, -75f);
        static readonly Vector3 InteriorOffset = new Vector3(0f, 8f, -12f);

        // Where the first view of a space with a player start looks from, turned with the start: behind it and above.
        static readonly Vector3 StartViewOffset = new Vector3(0f, 4.5f, -6f);

        readonly Dictionary<RecordId, Pose> _poses = new Dictionary<RecordId, Pose>();

        /// <param name="pose">The camera's pose, its position in world coordinates.</param>
        public void Remember(RecordId space, Pose pose) => _poses[space] = pose;

        /// <summary>The poses remembered so far, by space (the editor keeps them between sessions).</summary>
        public IReadOnlyDictionary<RecordId, Pose> Poses => _poses;

        /// <summary>The camera's last pose in <paramref name="space"/>, if it has been there.</summary>
        public bool TryRecall(RecordId space, out Pose pose) => _poses.TryGetValue(space, out pose);

        /// <summary>
        /// The camera's last pose in <paramref name="space"/>, or the first view of it: from behind its player start, looking
        /// down at where the player would stand (so playing from here begins there), else the default view of its middle.
        /// The position in world coordinates.
        /// </summary>
        public Pose Recall(SpaceRecord space)
        {
            if (TryRecall(space.Id, out var pose))
                return pose;
            if (space.PlayerStart != null)
                return Behind(space.PlayerStart);

            var offset = space.Kind == SpaceKind.Interior ? InteriorOffset : WorldspaceOffset + Vector3.up * space.BaseHeight;
            return new Pose(offset, Overview);
        }

        // Above and behind the start, the view's middle on the start's feet.
        static Pose Behind(PlayerStart start)
        {
            var feet = start.Position.ToUnity();
            var facing = Quaternion.Euler(0f, start.Heading, 0f);
            var camera = feet + facing * StartViewOffset;
            return new Pose(camera, Quaternion.LookRotation(feet - camera));
        }
    }
}
