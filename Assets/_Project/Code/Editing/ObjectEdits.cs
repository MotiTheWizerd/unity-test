using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Placement;

namespace Forge.Editing
{
    /// <summary>
    /// Multi-object operations on placed objects. Each call is one undo step.
    /// Ids that are not placed objects are ignored, so callers can pass a mixed selection.
    /// </summary>
    public static class ObjectEdits
    {
        /// <summary>Name prefix of the ids new placed objects get in the active plugin, e.g. "user:ref/000042".</summary>
        public const string PlacedIdPrefix = "ref";

        /// <summary>
        /// Creates a placed object in <paramref name="space"/> for each of <paramref name="placements"/> as one undo
        /// step described with <paramref name="description"/>. Returns the new records, in the same order.
        /// </summary>
        public static IReadOnlyList<PlacedObjectRecord> Place(EditSession session, RecordId space, IReadOnlyList<ObjectPlacement> placements, string description)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (space.IsDefault)
                throw new ArgumentException("A space to place in is required.", nameof(space));
            if (placements == null)
                throw new ArgumentNullException(nameof(placements));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("A description is required for the undo step.", nameof(description));
            if (placements.Count == 0)
                return Array.Empty<PlacedObjectRecord>();

            // All ids up front: each id stays free until its record is put.
            var ids = session.NewIds(PlacedIdPrefix, placements.Count);
            var records = placements
                .Select((p, i) => new PlacedObjectRecord(ids[i], p.BaseId, space, p.Pose.Position, p.Pose.Rotation, p.Scale))
                .ToList();
            session.Batch(description, () =>
            {
                foreach (var record in records)
                    session.Put(record);
            });
            return records;
        }

        /// <summary>
        /// Copies every placed object in <paramref name="ids"/>: same kind, space, transform and materials, a new id each,
        /// then set down by <paramref name="place"/> (where the copy goes; null: where the original stands), as one undo
        /// step described with <paramref name="description"/>. Ids that aren't placed objects are skipped. Returns the
        /// copies, in the order of <paramref name="ids"/>.
        /// </summary>
        public static IReadOnlyList<PlacedObjectRecord> Duplicate(EditSession session, IEnumerable<RecordId> ids, string description,
            Func<PlacedObjectRecord, PlacedObjectRecord> place = null)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("A description is required for the undo step.", nameof(description));

            var originals = new List<PlacedObjectRecord>();
            foreach (var id in ids.Distinct())
            {
                if (session.World.Records.TryGet(id, out var record) && record is PlacedObjectRecord placed)
                    originals.Add(placed);
            }

            if (originals.Count == 0)
                return Array.Empty<PlacedObjectRecord>();

            // All ids up front: each id stays free until its record is put.
            var newIds = session.NewIds(PlacedIdPrefix, originals.Count);
            var copies = originals
                .Select((p, i) => new PlacedObjectRecord(newIds[i], p.BaseId, p.Space, p.Position, p.Rotation, p.Scale, p.Materials))
                .Select(copy => place != null ? place(copy) : copy)
                .ToList();
            session.Batch(description, () =>
            {
                foreach (var copy in copies)
                    session.Put(copy);
            });
            return copies;
        }

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
        public static int Transform(EditSession session, IEnumerable<RecordId> ids, Func<PlacedObjectRecord, PlacedObjectRecord> change, string verb) =>
            Change(session, ids, change, (a, b) => a.HasSameTransform(b), verb);

        /// <summary>
        /// Dresses <paramref name="slot"/> of every placed object in <paramref name="ids"/> in
        /// <paramref name="material"/>, or gives it back its base object's look when that is null, as one undo step.
        /// Objects already wearing it are skipped. Which objects have the slot is the caller's to know (it depends on
        /// their assets); an object without it would keep the override harmlessly unseen. Returns how many changed.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="material"/> is not a material record of the world.</exception>
        public static int SetMaterial(EditSession session, IEnumerable<RecordId> ids, string slot, RecordId? material)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("A slot is required.", nameof(slot));
            if (material is RecordId id && !(session.World.Records.TryGet(id, out var record) && record is MaterialRecord))
                throw new ArgumentException($"There is no material '{id}'.", nameof(material));

            return Change(session, ids, p => p.WithMaterial(slot, material), (a, b) => a.HasSameMaterials(b),
                material.HasValue ? "Set material of" : "Reset material of");
        }

        /// <summary>Gives every placed object in <paramref name="ids"/> back its base object's look in every slot, as one undo step.</summary>
        public static int ResetMaterials(EditSession session, IEnumerable<RecordId> ids) =>
            Change(session, ids, p => p.WithMaterials(null), (a, b) => a.HasSameMaterials(b), "Reset materials of");

        /// <summary>Name prefix of the ids variants get in the active plugin, e.g. "user:variant/000003".</summary>
        public const string VariantIdPrefix = "variant";

        /// <summary>
        /// Saves what placed object <paramref name="placedId"/> looks like as a new kind of object, a variant of its base
        /// object named <paramref name="displayName"/>: the same asset, category, placement rules and parts (a variant of an
        /// item is an item), wearing the base object's materials with the placed object's own on top. The placed object
        /// becomes an instance of the variant (its own materials now come from it). One undo step. Returns the variant's id.
        /// </summary>
        /// <exception cref="ArgumentException">There is no such placed object, its base object is missing, or the name is blank.</exception>
        public static RecordId MakeVariant(EditSession session, RecordId placedId, string displayName)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A variant needs a name.", nameof(displayName));
            var records = session.World.Records;
            if (!(records.TryGet(placedId, out var record) && record is PlacedObjectRecord placed))
                throw new ArgumentException($"There is no placed object '{placedId}'.", nameof(placedId));
            if (!(records.TryGet(placed.BaseId, out var kind) && kind is BaseObjectRecord baseObject))
                throw new ArgumentException($"The kind of '{placedId}', {placed.BaseId}, no longer exists.", nameof(placedId));

            var name = displayName.Trim();
            var variant = new BaseObjectRecord(session.NewId(VariantIdPrefix), name, baseObject.AssetKey, baseObject.Category,
                baseObject.Placement, MaterialOverrides.Layer(baseObject.Materials, placed.Materials), baseObject.Item, baseObject.Equippable, baseObject.Weapon, baseObject.Actor,
                baseObject.Ammo, baseObject.Physics);
            session.Batch($"Make variant {name}", () =>
            {
                session.Put(variant);
                session.Put(placed.WithBase(variant.Id));
            });
            return variant.Id;
        }

        // The one commit path for changes to existing placed objects: one undo step, unchanged objects skipped.
        static int Change(EditSession session, IEnumerable<RecordId> ids, Func<PlacedObjectRecord, PlacedObjectRecord> change,
            Func<PlacedObjectRecord, PlacedObjectRecord, bool> same, string verb)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (change == null)
                throw new ArgumentNullException(nameof(change));
            if (string.IsNullOrWhiteSpace(verb))
                throw new ArgumentException("A verb is required to describe the edit.", nameof(verb));

            var changes = PlacedObjects(session, ids)
                .Select(original => (Original: original, Changed: change(original)))
                .Where(c => !same(c.Original, c.Changed))
                .ToList();
            if (changes.Count == 0)
                return 0;
            if (changes.Any(c => c.Changed.Id != c.Original.Id || c.Changed.BaseId != c.Original.BaseId))
                throw new ArgumentException("A change must keep the record's id and base object.", nameof(change));

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
