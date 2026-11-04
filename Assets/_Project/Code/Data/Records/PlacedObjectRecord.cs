using System;
using System.Numerics;

namespace Forge.Data.Records
{
    /// <summary>
    /// An instance of a <see cref="BaseObjectRecord"/> placed in a space; its position is local to that space.
    /// </summary>
    public sealed class PlacedObjectRecord : Record
    {
        public PlacedObjectRecord(RecordId id, RecordId baseId, RecordId space, Vector3 position, Quaternion rotation, float scale)
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
        }

        public RecordId BaseId { get; }

        /// <summary>The space the object stands in.</summary>
        public RecordId Space { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public float Scale { get; }

        public PlacedObjectRecord WithPosition(Vector3 position) => WithTransform(position, Rotation, Scale);

        public PlacedObjectRecord WithRotation(Quaternion rotation) => WithTransform(Position, rotation, Scale);

        public PlacedObjectRecord WithScale(float scale) => WithTransform(Position, Rotation, scale);

        public PlacedObjectRecord WithTransform(Vector3 position, Quaternion rotation, float scale) =>
            new PlacedObjectRecord(Id, BaseId, Space, position, rotation, scale);

        /// <summary>True when position, rotation and scale are exactly equal.</summary>
        public bool HasSameTransform(PlacedObjectRecord other) =>
            other != null && Position == other.Position && Rotation == other.Rotation && Scale == other.Scale;

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
