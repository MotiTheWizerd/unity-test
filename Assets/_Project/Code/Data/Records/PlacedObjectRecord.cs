using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Forge.Data.Records
{
    /// <summary>
    /// An instance of a <see cref="BaseObjectRecord"/> placed in a space; its position is local to that space. It may
    /// wear other materials than its base object in some of its slots (<see cref="Materials"/>); every other slot keeps
    /// the base object's look.
    /// </summary>
    public sealed class PlacedObjectRecord : Record
    {
        /// <summary>Most slots one object can dress differently.</summary>
        public const int MaxMaterials = 64;

        static readonly IReadOnlyList<MaterialOverride> NoMaterials = Array.Empty<MaterialOverride>();

        /// <param name="materials">Slots dressed in another material; null or empty: the base object's look throughout.</param>
        public PlacedObjectRecord(RecordId id, RecordId baseId, RecordId space, Vector3 position, Quaternion rotation, float scale,
            IEnumerable<MaterialOverride> materials = null)
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
            Materials = Sorted(materials);
        }

        public RecordId BaseId { get; }

        /// <summary>The space the object stands in.</summary>
        public RecordId Space { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public float Scale { get; }

        /// <summary>The slots dressed in another material than the base object's, sorted by slot name; empty when none are.</summary>
        public IReadOnlyList<MaterialOverride> Materials { get; }

        /// <summary>The material worn in <paramref name="slot"/> instead of the base object's, or null when it keeps that.</summary>
        public RecordId? MaterialIn(string slot)
        {
            foreach (var material in Materials)
            {
                if (string.Equals(material.Slot, slot, StringComparison.Ordinal))
                    return material.Material;
            }

            return null;
        }

        public PlacedObjectRecord WithPosition(Vector3 position) => WithTransform(position, Rotation, Scale);

        public PlacedObjectRecord WithRotation(Quaternion rotation) => WithTransform(Position, rotation, Scale);

        public PlacedObjectRecord WithScale(float scale) => WithTransform(Position, Rotation, scale);

        public PlacedObjectRecord WithTransform(Vector3 position, Quaternion rotation, float scale) =>
            new PlacedObjectRecord(Id, BaseId, Space, position, rotation, scale, Materials);

        /// <summary>A copy wearing <paramref name="material"/> in <paramref name="slot"/>; null gives the slot back its base look.</summary>
        public PlacedObjectRecord WithMaterial(string slot, RecordId? material)
        {
            var kept = Materials.Where(m => !string.Equals(m.Slot, slot, StringComparison.Ordinal));
            return WithMaterials(material is RecordId id ? kept.Append(new MaterialOverride(slot, id)) : kept);
        }

        /// <summary>A copy wearing exactly <paramref name="materials"/>; null or empty: the base object's look throughout.</summary>
        public PlacedObjectRecord WithMaterials(IEnumerable<MaterialOverride> materials) =>
            new PlacedObjectRecord(Id, BaseId, Space, Position, Rotation, Scale, materials);

        /// <summary>True when position, rotation and scale are exactly equal.</summary>
        public bool HasSameTransform(PlacedObjectRecord other) =>
            other != null && Position == other.Position && Rotation == other.Rotation && Scale == other.Scale;

        /// <summary>True when both wear the same materials in the same slots.</summary>
        public bool HasSameMaterials(PlacedObjectRecord other) =>
            other != null && Materials.SequenceEqual(other.Materials);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static IReadOnlyList<MaterialOverride> Sorted(IEnumerable<MaterialOverride> materials)
        {
            if (materials == null)
                return NoMaterials;

            var sorted = materials.OrderBy(m => m.Slot, StringComparer.Ordinal).ToArray();
            if (sorted.Length == 0)
                return NoMaterials;
            if (sorted.Length > MaxMaterials)
                throw new ArgumentException($"An object dresses at most {MaxMaterials} slots, not {sorted.Length}.", nameof(materials));
            for (var i = 0; i < sorted.Length; i++)
            {
                if (sorted[i].Slot == null)
                    throw new ArgumentException("A material override needs a slot.", nameof(materials));
                if (i > 0 && sorted[i - 1].Slot == sorted[i].Slot)
                    throw new ArgumentException($"Slot '{sorted[i].Slot}' is dressed more than once.", nameof(materials));
            }

            return sorted;
        }
    }
}
