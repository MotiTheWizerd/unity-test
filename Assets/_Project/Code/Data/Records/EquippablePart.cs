using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Records
{
    /// <summary>
    /// What makes an item something an actor equips and holds or wears: the ways it fits (<see cref="Fits"/>, each the
    /// <see cref="EquipSlotRecord"/>s it takes together) and what it does to its bearer's attributes while equipped
    /// (<see cref="Modifiers"/>). A part beside the <see cref="ItemPart"/>, so a mod makes anything equippable by giving
    /// it one. Which way it is held is the game's choice when it is equipped, and its look follows the slots' bones.
    /// </summary>
    public sealed class EquippablePart
    {
        /// <param name="fits">The ways it can be equipped, in order of preference (the first that is free is taken).</param>
        /// <param name="modifiers">What it does to its bearer's attributes while equipped; null or empty for nothing.</param>
        public EquippablePart(IEnumerable<EquipFit> fits, IEnumerable<AttributeModifier> modifiers = null)
        {
            var fitList = fits?.ToArray() ?? throw new ArgumentNullException(nameof(fits));
            if (fitList.Length == 0)
                throw new ArgumentException("Something equippable fits at least one way.", nameof(fits));
            if (fitList.Any(fit => fit == null))
                throw new ArgumentException("A fit can't be missing.", nameof(fits));
            for (var i = 0; i < fitList.Length; i++)
            {
                for (var j = i + 1; j < fitList.Length; j++)
                {
                    if (fitList[i].SameAs(fitList[j]))
                        throw new ArgumentException("Each fit is listed once.", nameof(fits));
                }
            }

            var modifierList = modifiers?.ToArray();
            if (modifierList != null && modifierList.Any(modifier => modifier == null))
                throw new ArgumentException("A modifier can't be missing.", nameof(modifiers));

            Fits = fitList;
            Modifiers = modifierList != null && modifierList.Length > 0 ? modifierList : null;
        }

        /// <summary>The ways it can be equipped, in order of preference; never empty.</summary>
        public IReadOnlyList<EquipFit> Fits { get; }

        /// <summary>What it does to its bearer's attributes while equipped; null when nothing (and left out of the file).</summary>
        public IReadOnlyList<AttributeModifier> Modifiers { get; }

        // No derived properties here: serializers write every public property, and a plugin file holds data only.
    }
}
