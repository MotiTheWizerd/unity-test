using System;
using System.Numerics;

namespace Forge.Data.Records
{
    /// <summary>
    /// An instance of a <see cref="BaseObjectRecord"/> placed in the world.
    /// </summary>
    public sealed class PlacedObjectRecord : Record
    {
        public PlacedObjectRecord(RecordId id, RecordId baseId, Vector3 position, Quaternion rotation, float scale)
            : base(id)
        {
            if (baseId.IsDefault)
                throw new ArgumentException("Base object id is required.", nameof(baseId));
            if (!(scale > 0f) || float.IsInfinity(scale))
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "Scale must be a positive finite number.");

            BaseId = baseId;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public RecordId BaseId { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public float Scale { get; }
    }
}
