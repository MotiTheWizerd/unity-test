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
            if (delta == Vector3.Zero)
                return 0;

            var positions = PlacedObjects(session, ids).ToDictionary(p => p.Id, p => p.Position + delta);
            return SetPositions(session, positions, "Move");
        }

        /// <summary>
        /// Sets absolute positions for placed objects (e.g. drop to ground) as one undo step
        /// described with <paramref name="verb"/>. Objects already at their target are skipped.
        /// Returns how many moved.
        /// </summary>
        public static int SetPositions(EditSession session, IReadOnlyDictionary<RecordId, Vector3> positions, string verb)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (positions == null)
                throw new ArgumentNullException(nameof(positions));
            if (string.IsNullOrWhiteSpace(verb))
                throw new ArgumentException("A verb is required to describe the edit.", nameof(verb));

            var targets = PlacedObjects(session, positions.Keys)
                .Where(p => p.Position != positions[p.Id])
                .ToList();
            if (targets.Count == 0)
                return 0;

            session.Batch(Describe(verb, targets), () =>
            {
                foreach (var placed in targets)
                    session.Put(placed.WithPosition(positions[placed.Id]));
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
