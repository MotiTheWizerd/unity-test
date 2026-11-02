using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;

namespace Forge.Editing.Terrain
{
    /// <summary>Commits terrain work to the session.</summary>
    public static class TerrainEdits
    {
        /// <summary>
        /// Writes every chunk the stroke changed, and the objects <paramref name="follower"/>
        /// moved with the ground (if given), as one undo step. Returns the number of chunks written.
        /// </summary>
        public static int Commit(EditSession session, SculptStroke stroke, TerrainFollower follower = null)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (stroke == null)
                throw new ArgumentNullException(nameof(stroke));

            var chunks = stroke.ChangedRecords();
            if (chunks.Count == 0)
                return 0;

            var moved = follower == null
                ? new List<PlacedObjectRecord>()
                : follower.Follow(stroke).Where(placed => IsMove(session, placed)).ToList();

            session.Batch($"Sculpt ({stroke.Brush.Mode})", () =>
            {
                foreach (var chunk in chunks)
                    session.Put(chunk);
                foreach (var placed in moved)
                    session.Put(placed);
            });
            return chunks.Count;
        }

        // Only objects that still exist and actually change position are written.
        static bool IsMove(EditSession session, PlacedObjectRecord placed) =>
            session.World.Records.TryGet(placed.Id, out var current)
            && current is PlacedObjectRecord existing
            && !existing.HasSameTransform(placed);
    }
}
