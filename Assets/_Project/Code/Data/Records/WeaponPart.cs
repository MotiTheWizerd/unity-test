using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Records
{
    /// <summary>
    /// What makes an equippable item a weapon: how it is fought with (<see cref="Style"/>, a <see cref="CombatStyleRecord"/>:
    /// its moves) and what a blow with it does to whoever it strikes (<see cref="Hit"/>). A part beside the
    /// <see cref="EquippablePart"/>, so a sword, an axe and a staff are the same kind of data, apart in their style. A
    /// weapon that shoots (a bow) names where what it shoots is equipped (<see cref="Ammo"/>): its blow is then what it
    /// looses, not itself.
    /// </summary>
    public sealed class WeaponPart
    {
        /// <param name="style">How it is fought with: the combat style record.</param>
        /// <param name="hit">What a blow does to whoever it strikes; at least one effect.</param>
        /// <param name="ammo">The equip slot of what it shoots (a bow's arrows); null for a weapon that strikes with itself.</param>
        public WeaponPart(RecordId style, IEnumerable<Effect> hit, RecordId? ammo = null)
        {
            if (style.IsDefault)
                throw new ArgumentException("A weapon names its combat style.", nameof(style));
            if (ammo.HasValue && ammo.Value.IsDefault)
                throw new ArgumentException("A weapon's ammo is an equip slot, or none.", nameof(ammo));
            var effects = hit?.ToArray() ?? throw new ArgumentNullException(nameof(hit));
            if (effects.Length == 0)
                throw new ArgumentException("A weapon's blow does something: give it an effect.", nameof(hit));
            if (effects.Any(effect => effect == null))
                throw new ArgumentException("An effect can't be missing.", nameof(hit));

            Style = style;
            Hit = effects;
            Ammo = ammo;
        }

        public RecordId Style { get; }

        /// <summary>What a blow does to whoever it strikes, in order; never empty.</summary>
        public IReadOnlyList<Effect> Hit { get; }

        /// <summary>The equip slot of what it shoots (a bow's arrows); null for a weapon that strikes with itself (and left out of the file).</summary>
        public RecordId? Ammo { get; }

        // No derived properties here: serializers write every public property, and a plugin file holds data only.
    }
}
