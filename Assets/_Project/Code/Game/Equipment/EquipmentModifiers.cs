using System;
using System.Collections.Generic;
using Forge.Game.Attributes;

namespace Forge.Game.Equipment
{
    /// <summary>
    /// What an actor's equipment does to its attributes: each thing equipped adds its
    /// <see cref="Forge.Data.Records.EquippablePart.Modifiers"/> under a source of its own, and taking it off takes exactly
    /// those away, so two rings of strength add twice and losing one leaves the other's. Bound while the actor has
    /// attributes (the player while spawned); disposing takes every modifier it added away. A modifier for an attribute
    /// the world lacks does nothing, as an item whose plugin is gone weighs nothing. Engine-free.
    /// </summary>
    public sealed class EquipmentModifiers : IDisposable
    {
        readonly ActorEquipment _equipment;
        readonly ActorAttributes _attributes;
        readonly Dictionary<EquippedItem, ModifierSource> _sources = new Dictionary<EquippedItem, ModifierSource>();

        public EquipmentModifiers(ActorEquipment equipment, ActorAttributes attributes)
        {
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _equipment.Equipped += Apply;
            _equipment.Unequipped += Take;
            foreach (var equipped in _equipment.All)
                Apply(equipped);
        }

        public void Dispose()
        {
            _equipment.Equipped -= Apply;
            _equipment.Unequipped -= Take;
            foreach (var source in _sources.Values)
                _attributes.Remove(source);
            _sources.Clear();
        }

        void Apply(EquippedItem equipped)
        {
            var modifiers = equipped.Record.Equippable.Modifiers;
            if (modifiers == null)
                return;

            // A slot holds one thing at a time, so its first slot names it among what is equipped now.
            var source = new ModifierSource("equipped/" + equipped.Fit.Slots[0]);
            _sources[equipped] = source;
            foreach (var modifier in modifiers)
            {
                if (_attributes.Set.Contains(modifier.Attribute))
                    _attributes.Add(source, modifier);
            }
        }

        void Take(EquippedItem equipped, UnequipReason reason)
        {
            if (!_sources.TryGetValue(equipped, out var source))
                return;
            _sources.Remove(equipped);
            _attributes.Remove(source);
        }
    }
}
