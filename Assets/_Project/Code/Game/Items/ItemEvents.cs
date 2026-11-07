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
}
