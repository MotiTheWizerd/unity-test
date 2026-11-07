using System;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Game.Attributes;
using Forge.Game.Events;
using Forge.Game.Player;

namespace Forge.Game.Items
{
    /// <summary>
    /// What an actor carries against what it can (<see cref="CoreAttributes.CarryWeight"/>), as Skyrim's
    /// over-encumbrance: past it the actor walks whatever it is told, no run and no sprint (<see cref="Allow"/>). The
    /// weight is worked out again only when the inventory or the carry weight changes (an item taken, a potion of
    /// strength), and each change of state is told on the stream (<see cref="ActorOverburdened"/>,
    /// <see cref="ActorUnburdened"/>). Engine-free.
    /// </summary>
    public sealed class Encumbrance : IMovementLimit, IDisposable
    {
        readonly Inventory _inventory;
        readonly IRecordStore _records;
        readonly ActorAttributes _attributes;
        readonly GameEvents _events;

        /// <param name="records">The world the items are read from, for their weights.</param>
        /// <param name="events">The game's stream, told when the actor becomes and stops being overburdened; null to tell no one.</param>
        public Encumbrance(Inventory inventory, IRecordStore records, ActorAttributes attributes, GameEvents events = null)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _records = records ?? throw new ArgumentNullException(nameof(records));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            if (!attributes.Set.Contains(CoreAttributes.CarryWeight))
                throw new DataValidationException(new[] { $"Carrying needs the attribute {CoreAttributes.CarryWeight}, which the world lacks." });
            _events = events;

            _inventory.Changed += Update;
            _attributes.Changed += OnAttributeChanged;
            Update();
        }

        /// <summary>What the actor carries now.</summary>
        public float Carried { get; private set; }

        /// <summary>What it can carry before it is slowed.</summary>
        public float Capacity => _attributes.Value(CoreAttributes.CarryWeight);

        /// <summary>Carrying more than it can: walking only.</summary>
        public bool Overburdened { get; private set; }

        /// <summary>What it carries or can carry changed, for whatever shows the two.</summary>
        public event Action Changed;

        /// <summary>The commands as an actor carrying this much can follow them: an overburdened one walks.</summary>
        public PlayerCommands Allow(PlayerCommands commands) =>
            Overburdened ? commands.Allowing(false, commands.Jump, true) : commands;

        public void Dispose()
        {
            _inventory.Changed -= Update;
            _attributes.Changed -= OnAttributeChanged;
        }

        void OnAttributeChanged(AttributeChange change)
        {
            if (change.Attribute == CoreAttributes.CarryWeight)
                Update();
        }

        void Update()
        {
            Carried = _inventory.Weight(_records);
            var over = Carried > Capacity;
            if (over != Overburdened)
            {
                Overburdened = over;
                if (over)
                    _events?.Publish(new ActorOverburdened(_attributes.Owner, Carried, Capacity));
                else
                    _events?.Publish(new ActorUnburdened(_attributes.Owner));
            }

            Changed?.Invoke();
        }
    }
}
