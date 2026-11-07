using System;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;

namespace Forge.Game.Interaction
{
    /// <summary>A placed object an actor has in its sights, and what it is: its base object, whose parts say what can be done with it.</summary>
    public readonly struct InteractionTarget
    {
        public InteractionTarget(PlacedObjectRecord placed, BaseObjectRecord kind)
        {
            Placed = placed ?? throw new ArgumentNullException(nameof(placed));
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        }

        public PlacedObjectRecord Placed { get; }

        public BaseObjectRecord Kind { get; }

        /// <summary>Whether this is no target at all (the default value).</summary>
        public bool IsNone => Placed == null;

        /// <summary>Whether both are the same object as the same records (records are immutable: a changed one is new).</summary>
        public bool Is(InteractionTarget other) => ReferenceEquals(Placed, other.Placed) && ReferenceEquals(Kind, other.Kind);

        /// <summary>The placed object <paramref name="id"/> as a target; false when there is no such placed object or its base object is missing.</summary>
        public static bool TryResolve(IRecordStore records, RecordId id, out InteractionTarget target)
        {
            target = default;
            if (records == null || !records.TryGet(id, out var record) || !(record is PlacedObjectRecord placed))
                return false;
            if (!records.TryGet(placed.BaseId, out var kind) || !(kind is BaseObjectRecord baseObject))
                return false;
            target = new InteractionTarget(placed, baseObject);
            return true;
        }
    }
}
