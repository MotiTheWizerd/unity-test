using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Forge.Data.Records
{
    /// <summary>
    /// An instance of a <see cref="BaseObjectRecord"/> placed in a space; its position is local to that space. It may
    /// wear other materials than its base object in some of its slots (<see cref="Materials"/>); every other slot keeps
    /// the base object's look. A character may be placed dead (<see cref="StartsDead"/>), as the Creation Kit's
    /// reference flag has it: a body lying where it fell, from the first.
    /// </summary>
    public sealed class PlacedObjectRecord : Record
    {
        /// <param name="materials">Slots dressed in another material; null or empty: the base object's look throughout.</param>
        /// <param name="startsDead">A character placed dead: it lies where it is, a body, from the first; nothing else minds it.</param>
        public PlacedObjectRecord(RecordId id, RecordId baseId, RecordId space, Vector3 position, Quaternion rotation, float scale,
            IEnumerable<MaterialOverride> materials = null, bool startsDead = false)
            : base(id)
        {
            if (baseId.IsDefault)
                throw new ArgumentException("Base object id is required.", nameof(baseId));
            if (space.IsDefault)
                throw new ArgumentException("The space the object stands in is required.", nameof(space));
            if (!IsFinite(position.X) || !IsFinite(position.Y) || !IsFinite(position.Z))
                throw new ArgumentOutOfRangeException(nameof(position), position, "Position must be finite.");
            if (!IsFinite(rotation.X) || !IsFinite(rotation.Y) || !IsFinite(rotation.Z) || !IsFinite(rotation.W))
                throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "Rotation must be finite.");
            if (!(scale > 0f) || float.IsInfinity(scale))
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "Scale must be a positive finite number.");

            BaseId = baseId;
            Space = space;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Materials = MaterialOverrides.Validated(materials, nameof(materials));
            StartsDead = startsDead;
        }

        public RecordId BaseId { get; }

        /// <summary>The space the object stands in.</summary>
        public RecordId Space { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public float Scale { get; }

        /// <summary>The slots dressed in another material than the base object's, sorted by slot name; empty when none are.</summary>
        public IReadOnlyList<MaterialOverride> Materials { get; }

        /// <summary>Whether it is a character placed dead: a body lying where it is from the first (the Creation Kit's "Starts Dead").</summary>
        public bool StartsDead { get; }

        /// <summary>The material worn in <paramref name="slot"/> instead of the base object's, or null when it keeps that.</summary>
        public RecordId? MaterialIn(string slot) => MaterialOverrides.In(Materials, slot);

        /// <summary>A copy under another id (a duplicate), all else as it is.</summary>
        public PlacedObjectRecord WithId(RecordId id) => new PlacedObjectRecord(id, BaseId, Space, Position, Rotation, Scale, Materials, StartsDead);

        public PlacedObjectRecord WithPosition(Vector3 position) => WithTransform(position, Rotation, Scale);

        public PlacedObjectRecord WithRotation(Quaternion rotation) => WithTransform(Position, rotation, Scale);

        public PlacedObjectRecord WithScale(float scale) => WithTransform(Position, Rotation, scale);

        public PlacedObjectRecord WithTransform(Vector3 position, Quaternion rotation, float scale) =>
            new PlacedObjectRecord(Id, BaseId, Space, position, rotation, scale, Materials, StartsDead);

        /// <summary>A copy placed dead (<paramref name="startsDead"/>), or alive.</summary>
        public PlacedObjectRecord WithStartsDead(bool startsDead) =>
            new PlacedObjectRecord(Id, BaseId, Space, Position, Rotation, Scale, Materials, startsDead);

        /// <summary>A copy wearing <paramref name="material"/> in <paramref name="slot"/>; null gives the slot back its base look.</summary>
        public PlacedObjectRecord WithMaterial(string slot, RecordId? material)
        {
            var kept = Materials.Where(m => !string.Equals(m.Slot, slot, StringComparison.Ordinal));
            return WithMaterials(material is RecordId id ? kept.Append(new MaterialOverride(slot, id)) : kept);
        }

        /// <summary>A copy wearing exactly <paramref name="materials"/>; null or empty: the base object's look throughout.</summary>
        public PlacedObjectRecord WithMaterials(IEnumerable<MaterialOverride> materials) =>
            new PlacedObjectRecord(Id, BaseId, Space, Position, Rotation, Scale, materials, StartsDead);

        /// <summary>A copy that is an instance of <paramref name="baseId"/> instead, wearing <paramref name="materials"/> (null: none of its own).</summary>
        public PlacedObjectRecord WithBase(RecordId baseId, IEnumerable<MaterialOverride> materials = null) =>
            new PlacedObjectRecord(Id, baseId, Space, Position, Rotation, Scale, materials, StartsDead);

        /// <summary>True when position, rotation and scale are exactly equal.</summary>
        public bool HasSameTransform(PlacedObjectRecord other) =>
            other != null && Position == other.Position && Rotation == other.Rotation && Scale == other.Scale;

        /// <summary>True when both wear the same materials in the same slots.</summary>
        public bool HasSameMaterials(PlacedObjectRecord other) =>
            other != null && Materials.SequenceEqual(other.Materials);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
