using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;

namespace Forge.Editing
{
    /// <summary>
    /// Multi-object operations on placed objects. Each call is one undo step.
    /// Ids that are not placed objects are ignored, so callers can pass a mixed selection.
    /// </summary>
    public static class ObjectEdits
    {
        /// <summary>Moves every placed object in <paramref name="ids"/> by <paramref name="delta"/>. Returns how many moved.</summary>
        public static int Move(EditSession session, IEnumerable<RecordId> ids, Vector3 delta)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var targets = PlacedObjects(session, ids);
            if (targets.Count == 0 || delta == Vector3.Zero)
                return 0;

            session.Batch(Describe("Move", targets), () =>
            {
                foreach (var placed in targets)
                    session.Put(placed.WithPosition(placed.Position + delta));
            });
            return targets.Count;
        }

        /// <summary>Deletes every placed object in <paramref name="ids"/>. Returns how many were deleted.</summary>
        public static int Delete(EditSession session, IEnumerable<RecordId> ids)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var targets = PlacedObjects(session, ids);
            if (targets.Count == 0)
                return 0;

            session.Batch(Describe("Delete", targets), () =>
            {
                foreach (var placed in targets)
                    session.Delete(placed.Id);
            });
            return targets.Count;
        }

        /// <summary>Average position of the placed objects in <paramref name="ids"/>, or null if there are none.</summary>
        public static Vector3? Pivot(EditSession session, IEnumerable<RecordId> ids)
        {
            var targets = PlacedObjects(session, ids);
            if (targets.Count == 0)
                return null;
            return targets.Aggregate(Vector3.Zero, (sum, p) => sum + p.Position) / targets.Count;
        }

        static List<PlacedObjectRecord> PlacedObjects(EditSession session, IEnumerable<RecordId> ids) =>
            (ids ?? Enumerable.Empty<RecordId>())
                .Distinct()
                .Select(id => session.World.Records.TryGet(id, out var record) ? record as PlacedObjectRecord : null)
                .Where(p => p != null)
                .ToList();

        static string Describe(string verb, IReadOnlyCollection<PlacedObjectRecord> targets) =>
            targets.Count == 1 ? $"{verb} {targets.First().Id.Name}" : $"{verb} {targets.Count} objects";
    }
}
