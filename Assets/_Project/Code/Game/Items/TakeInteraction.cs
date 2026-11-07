using System;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Events;
using Forge.Game.Interaction;
using Forge.Game.World;

namespace Forge.Game.Items
{
    /// <summary>
    /// Taking an item, a placed object whose base object has an <see cref="ItemPart"/>: it leaves the world
    /// (<see cref="WorldChanges"/>) and joins what the actor carries, and the stream hears <see cref="ItemTaken"/>. The
    /// prompt reads the item's name, "Take", and its weight and value.
    /// </summary>
    public sealed class TakeInteraction : IInteraction
    {
        public const string Verb = "Take";

        readonly WorldChanges _world;
        readonly Func<ActorId, Inventory> _inventoryOf;
        readonly GameEvents _events;

        /// <param name="inventoryOf">What an actor carries; null for an actor that carries nothing, who is offered nothing.</param>
        public TakeInteraction(WorldChanges world, Func<ActorId, Inventory> inventoryOf, GameEvents events)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _inventoryOf = inventoryOf ?? throw new ArgumentNullException(nameof(inventoryOf));
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public InteractionOffer Offer(InteractionTarget target, ActorId actor)
        {
            var item = target.Kind?.Item;
            if (item == null || _world.IsRemoved(target.Placed.Id) || _inventoryOf(actor) == null)
                return null;
            return new InteractionOffer(Verb, target.Kind.DisplayName, Describe(item));
        }

        public void Perform(InteractionTarget target, ActorId actor)
        {
            var inventory = _inventoryOf(actor) ?? throw new InvalidOperationException($"{actor} carries nothing.");
            // Already gone (taken twice in one frame): nothing more to take.
            if (!_world.Remove(target.Placed.Id))
                return;
            inventory.Add(target.Kind.Id);
            _events.Publish(new ItemTaken(actor, target.Kind.Id, target.Placed.Id, 1));
        }

        /// <summary>The prompt's line for an item: "Weight 9   Value 75" (weight to a tenth).</summary>
        public static string Describe(ItemPart item) =>
            FormattableString.Invariant($"Weight {item.Weight:0.#}   Value {item.Value}");
    }
}
