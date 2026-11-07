using Forge.Data;
using Forge.Game.Actors;
using Forge.Game.Events;

namespace Forge.Game.Items
{
    /// <summary>An actor took an item out of the world into what it carries.</summary>
    public readonly struct ItemTaken : IGameEvent
    {
        public ItemTaken(ActorId actor, RecordId item, RecordId from, int count)
        {
            Actor = actor;
            Item = item;
            From = from;
            Count = count;
        }

        public ActorId Actor { get; }

        /// <summary>The item: its base object.</summary>
        public RecordId Item { get; }

        /// <summary>The placed object it was.</summary>
        public RecordId From { get; }

        public int Count { get; }

        public override string ToString() => $"{Actor}: took {Count} × {Item} ({From})";
    }

    /// <summary>An actor dropped an item it carried into the world.</summary>
    public readonly struct ItemDropped : IGameEvent
    {
        public ItemDropped(ActorId actor, RecordId item, RecordId placed, int count)
        {
            Actor = actor;
            Item = item;
            Placed = placed;
            Count = count;
        }

        public ActorId Actor { get; }

        /// <summary>The item: its base object.</summary>
        public RecordId Item { get; }

        /// <summary>The placed object it now is.</summary>
        public RecordId Placed { get; }

        public int Count { get; }

        public override string ToString() => $"{Actor}: dropped {Count} × {Item} ({Placed})";
    }

    /// <summary>An actor carries more than it can and can no longer run, as Skyrim's over-encumbered.</summary>
    public readonly struct ActorOverburdened : IGameEvent
    {
        public ActorOverburdened(ActorId actor, float carried, float capacity)
        {
            Actor = actor;
            Carried = carried;
            Capacity = capacity;
        }

        public ActorId Actor { get; }

        public float Carried { get; }

        public float Capacity { get; }

        public override string ToString() => $"{Actor}: overburdened, {Carried:0.#} of {Capacity:0.#}";
    }

    /// <summary>An overburdened actor carries what it can again and may run.</summary>
    public readonly struct ActorUnburdened : IGameEvent
    {
        public ActorUnburdened(ActorId actor) => Actor = actor;

        public ActorId Actor { get; }

        public override string ToString() => $"{Actor}: unburdened";
    }
}
