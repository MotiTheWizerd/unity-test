using System;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Events;
using Forge.Game.World;

namespace Forge.Game.Items
{
    /// <summary>
    /// Dropping an item, taking in reverse: one leaves what the actor carries and stands in the world as a placed object
    /// playing made (<see cref="WorldChanges.Add"/>), takeable again like any other, and the stream hears
    /// <see cref="ItemDropped"/>. Where it lands is the caller's to say (the engine finds the ground). Engine-free.
    /// </summary>
    public sealed class ItemDropper
    {
        readonly WorldChanges _world;
        readonly Func<ActorId, Inventory> _inventoryOf;
        readonly GameEvents _events;

        /// <param name="inventoryOf">What an actor carries; null for an actor that carries nothing.</param>
        public ItemDropper(WorldChanges world, Func<ActorId, Inventory> inventoryOf, GameEvents events)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _inventoryOf = inventoryOf ?? throw new ArgumentNullException(nameof(inventoryOf));
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        /// <summary>
        /// Drops one <paramref name="item"/> <paramref name="actor"/> carries in <paramref name="space"/> at
        /// <paramref name="position"/>, turned <paramref name="rotation"/>: the placed object it now is, or null when the
        /// actor carries none.
        /// </summary>
        public PlacedObjectRecord Drop(ActorId actor, RecordId item, RecordId space, Vector3 position, Quaternion rotation)
        {
            var inventory = _inventoryOf(actor) ?? throw new InvalidOperationException($"{actor} carries nothing.");
            if (inventory.CountOf(item) == 0)
                return null;

            // Placed before the item leaves the pack, so a bad place loses nothing.
            var placed = _world.Place(item, space, position, rotation);
            inventory.TryRemove(item);
            _events.Publish(new ItemDropped(actor, item, placed.Id, 1));
            return placed;
        }
    }
}
