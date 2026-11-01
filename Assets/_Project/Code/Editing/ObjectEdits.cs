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

            return Transform(session, ids, p => p.WithPosition(p.Position + delta), "Move");
        }

        /// <summary>
        /// Sets absolute positions for placed objects (e.g. drop to ground) as one undo step
        /// described with <paramref name="verb"/>. Objects already at their target are skipped.
        /// Returns how many moved.
        /// </summary>
        public static int SetPositions(EditSession session, IReadOnlyDictionary<RecordId, Vector3> positions, string verb)
        {
            if (positions == null)
                throw new ArgumentNullException(nameof(positions));

            return Transform(session, positions.Keys, p => p.WithPosition(positions[p.Id]), verb);
        }

        /// <summary>
        /// The one commit path for every transform (move, rotate, scale, drop):
        /// applies <paramref name="change"/> to each placed object in <paramref name="ids"/>
        /// as one undo step described with <paramref name="verb"/>. Objects whose transform
        /// doesn't change are skipped. Returns how many changed.
        /// </summary>
        public static int Transform(EditSession session, IEnumerable<RecordId> ids, Func<PlacedObjectRecord, PlacedObjectRecord> change, string verb)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (change == null)
                throw new ArgumentNullException(nameof(change));
            if (string.IsNullOrWhiteSpace(verb))
                throw new ArgumentException("A verb is required to describe the edit.", nameof(verb));

            var changes = PlacedObjects(session, ids)
                .Select(original => (Original: original, Changed: change(original)))
                .Where(c => !c.Original.HasSameTransform(c.Changed))
                .ToList();
            if (changes.Count == 0)
                return 0;
            if (changes.Any(c => c.Changed.Id != c.Original.Id || c.Changed.BaseId != c.Original.BaseId))
                throw new ArgumentException("A transform change must keep the record's id and base object.", nameof(change));

            session.Batch(Describe(verb, changes.Select(c => c.Original).ToList()), () =>
            {
                foreach (var c in changes)
                    session.Put(c.Changed);
            });
            return changes.Count;
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
