using System;
using System.Collections.Generic;

namespace Forge.Data.Records
{
    /// <summary>
    /// A definition of something that can be placed in the world (a tree, a rock,
    /// a chair). Placed instances reference it by id, so shared data lives once.
    /// What it is beyond its look comes in optional parts: an <see cref="ItemPart"/> makes it something to carry, an
    /// <see cref="EquippablePart"/> something its carrier can equip, a <see cref="WeaponPart"/> something to fight with, and
    /// an <see cref="AmmoPart"/> something a weapon shoots, a <see cref="BlockPart"/> something to block blows with (a
    /// shield), and a <see cref="PhysicsPart"/> a loose physical thing that is no item. What it is made of
    /// (<see cref="Surface"/>) says how it sounds struck.
    /// </summary>
    public sealed class BaseObjectRecord : Record
    {
        /// <param name="category">Where the palette files it ("Nature/Trees"); null or blank = uncategorized.</param>
        /// <param name="placement">How the editor places new instances; null = exactly as chosen, upright.</param>
        /// <param name="materials">Slots every instance wears another material in than its asset does (a variant's look); null or empty = none.</param>
        /// <param name="item">What makes it an item to pick up and carry; null = not an item.</param>
        /// <param name="equippable">What makes it something to equip and hold or wear; null = not equippable. Only an item is.</param>
        /// <param name="weapon">What makes it a weapon to fight with; null = not a weapon. Only something equippable is.</param>
        /// <param name="actor">The actor each one placed is while the world plays (an <see cref="ActorRecord"/>: a training dummy, later a character); null = no actor.</param>
        /// <param name="ammo">What makes it ammunition a weapon shoots; null = not ammo. Only an item is.</param>
        /// <param name="physics">What makes it a loose physical thing while playing, with its mass; null = an item is by its weight, anything else stands still.</param>
        /// <param name="block">How well it blocks a blow held up against it (a shield); null = it doesn't. Only a weapon does.</param>
        /// <param name="surface">What it is made of to strike (<see cref="Surfaces"/>: "wood", "metal"); null = nothing in particular.</param>
        public BaseObjectRecord(RecordId id, string displayName, string assetKey, string category = null, PlacementRules placement = null,
            IEnumerable<MaterialOverride> materials = null, ItemPart item = null, EquippablePart equippable = null, WeaponPart weapon = null,
            RecordId? actor = null, AmmoPart ammo = null, PhysicsPart physics = null, BlockPart block = null, string surface = null)
            : base(id)
        {
            if (surface != null && !Surfaces.IsValid(surface))
                throw new ArgumentException($"A surface is a lowercase word ('wood', 'metal'), not '{surface}'.", nameof(surface));
            if (block != null && weapon == null)
                throw new ArgumentException("Only a weapon blocks (its style raises it): give it a weapon part too.", nameof(block));
            if (physics != null && actor.HasValue)
                throw new ArgumentException("An actor's body is its own, not a loose thing: give it one or the other.", nameof(physics));
            if (ammo != null && item == null)
                throw new ArgumentException("Only an item is ammunition: give it an item part too.", nameof(ammo));
            if (actor.HasValue && actor.Value.IsDefault)
                throw new ArgumentException("An actor link names the actor, or is left out.", nameof(actor));
            if (actor.HasValue && item != null)
                throw new ArgumentException("An item is carried, not an actor: give it one or the other.", nameof(actor));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(assetKey))
                throw new ArgumentException("Asset key is required.", nameof(assetKey));
            if (equippable != null && item == null)
                throw new ArgumentException("Only an item is equippable: give it an item part too.", nameof(equippable));
            if (weapon != null && equippable == null)
                throw new ArgumentException("Only something equippable is a weapon: give it an equippable part too.", nameof(weapon));

            DisplayName = displayName;
            AssetKey = assetKey;
            Category = CategoryPath.Normalize(category);
            Placement = placement;
            var validated = MaterialOverrides.Validated(materials, nameof(materials));
            Materials = validated.Count > 0 ? validated : null;
            Item = item;
            Equippable = equippable;
            Weapon = weapon;
            Actor = actor;
            Ammo = ammo;
            Physics = physics;
            Block = block;
            Surface = surface;
        }

        public string DisplayName { get; }

        /// <summary>Engine-agnostic key the presentation layer resolves to a visual asset.</summary>
        public string AssetKey { get; }

        /// <summary>Normalized <see cref="CategoryPath"/>; empty when uncategorized.</summary>
        public string Category { get; }

        /// <summary>Placement rules for new instances, or null: placed exactly as chosen, upright.</summary>
        public PlacementRules Placement { get; }

        /// <summary>
        /// The slots every instance wears another material in than the asset does, sorted by slot (an instance's own
        /// <see cref="PlacedObjectRecord.Materials"/> layer on top), or null: the asset's look. How a variant of an object
        /// differs from it, as in the Creation Kit.
        /// </summary>
        public IReadOnlyList<MaterialOverride> Materials { get; }

        /// <summary>What makes it an item an actor can pick up and carry, or null: not an item.</summary>
        public ItemPart Item { get; }

        /// <summary>What makes it something its carrier can equip, or null: not equippable.</summary>
        public EquippablePart Equippable { get; }

        /// <summary>What makes it a weapon to fight with, or null: not a weapon.</summary>
        public WeaponPart Weapon { get; }

        /// <summary>
        /// The actor each one placed is while the world plays (an <see cref="ActorRecord"/>), or null: just a thing. Its
        /// look is this object's; who it is (and, later, what it carries and how it fights) is the actor's.
        /// </summary>
        public RecordId? Actor { get; }

        /// <summary>What makes it ammunition a weapon shoots (an arrow), or null: not ammo.</summary>
        public AmmoPart Ammo { get; }

        /// <summary>What makes it a loose physical thing while playing when it is no item (a barrel), or null.</summary>
        public PhysicsPart Physics { get; }

        /// <summary>How well it blocks a blow held up against it (a shield), or null: it doesn't.</summary>
        public BlockPart Block { get; }

        /// <summary>
        /// What it is made of to strike ("wood", "metal"; <see cref="Surfaces"/>), which its sounds are found by (a
        /// wooden shield thuds where an iron one rings), or null: nothing in particular.
        /// </summary>
        public string Surface { get; }
    }
}
