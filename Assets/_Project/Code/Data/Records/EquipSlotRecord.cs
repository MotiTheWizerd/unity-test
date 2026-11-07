using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// A place on an actor where something is equipped, like a Bethesda equip slot: a hand now; later a head, a body, a
    /// ring finger. Data, not code: an <see cref="EquippablePart"/> names the slots it takes, and a mod adds a slot (a
    /// back for a quiver) by adding a record. Each slot holds one thing; dual wielding is two slots, a hand each.
    /// </summary>
    public sealed class EquipSlotRecord : Record
    {
        /// <summary>Name prefix of equip slot records, e.g. "forge.core:equip_slot/right_hand".</summary>
        public const string NamePrefix = "equip_slot/";

        /// <param name="bone">
        /// The humanoid bone what it holds hangs from while shown ("RightHand", as humanoid rigs name their bones); null
        /// when nothing in it is shown.
        /// </param>
        /// <param name="sheathes">
        /// Whether what it holds is put away until the actor readies it (a hand's sword sheathed at the hip, drawn with
        /// R), as Skyrim's weapons are; a ring is simply worn.
        /// </param>
        public EquipSlotRecord(RecordId id, string displayName, string bone = null, bool sheathes = false)
            : base(id)
        {
            if (!id.Name.StartsWith(NamePrefix, StringComparison.Ordinal))
                throw new ArgumentException($"Equip slot ids are named '{NamePrefix}…', not '{id.Name}'.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (bone != null && string.IsNullOrWhiteSpace(bone))
                throw new ArgumentException("A bone is named, or left out for none.", nameof(bone));

            DisplayName = displayName;
            Bone = bone;
            Sheathes = sheathes;
        }

        /// <summary>What a player reads ("Right Hand").</summary>
        public string DisplayName { get; }

        /// <summary>The humanoid bone what it holds hangs from; null when nothing in it is shown (and left out of the file).</summary>
        public string Bone { get; }

        /// <summary>Whether what it holds is put away until readied (sheathed, then drawn).</summary>
        public bool Sheathes { get; }

        public static RecordId IdFor(string ns, string name) => new RecordId(ns, NamePrefix + name);
    }
}
