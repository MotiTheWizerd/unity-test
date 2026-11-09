using System;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Equipment;
using Forge.Game.Items;

namespace Forge.Game.Hud
{
    /// <summary>
    /// What the HUD's ammo count says: while a weapon that shoots is out (a bow drawing or drawn), what it shoots and how
    /// many of it are carried; with none equipped, its ammo slot's name and none. Hidden otherwise (sheathed, a sword).
    /// Engine-free; read each frame (<see cref="Of"/>), a struct, so reading it allocates nothing.
    /// </summary>
    public readonly struct AmmoCount : IEquatable<AmmoCount>
    {
        /// <summary>Nothing to show.</summary>
        public static readonly AmmoCount Hidden = default;

        public AmmoCount(string name, int count)
        {
            Shown = true;
            Name = name;
            Count = count;
        }

        public bool Shown { get; }

        /// <summary>What is shot (Iron Arrow), or the slot it would be in (Ammo) when nothing is.</summary>
        public string Name { get; }

        public int Count { get; }

        /// <summary>
        /// The count for an actor with <paramref name="equipment"/>, carrying <paramref name="inventory"/>, its weapons as
        /// <paramref name="readiness"/> has them; the ammo slot's name is read from <paramref name="records"/>.
        /// </summary>
        public static AmmoCount Of(ActorEquipment equipment, Inventory inventory, WeaponReadiness readiness, IRecordStore records)
        {
            if (equipment == null)
                throw new ArgumentNullException(nameof(equipment));
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            if (readiness == null)
                throw new ArgumentNullException(nameof(readiness));
            if (records == null)
                throw new ArgumentNullException(nameof(records));
            if (readiness.State != Readiness.Drawing && readiness.State != Readiness.Drawn)
                return Hidden;

            foreach (var equipped in equipment.All)
            {
                var slot = equipped.Record.Weapon?.Ammo;
                if (slot == null)
                    continue;
                var ammo = equipment.In(slot.Value)?.Record;
                if (ammo != null)
                    return new AmmoCount(ammo.DisplayName, inventory.CountOf(ammo.Id));
                return new AmmoCount(records.TryGet(slot.Value, out var record) && record is EquipSlotRecord named ? named.DisplayName : null, 0);
            }

            return Hidden;
        }

        public bool Equals(AmmoCount other) => Shown == other.Shown && Name == other.Name && Count == other.Count;

        public override bool Equals(object obj) => obj is AmmoCount other && Equals(other);

        public override int GetHashCode() => ((Name?.GetHashCode() ?? 0) * 397 ^ Count) * 2 + (Shown ? 1 : 0);

        public static bool operator ==(AmmoCount a, AmmoCount b) => a.Equals(b);

        public static bool operator !=(AmmoCount a, AmmoCount b) => !a.Equals(b);

        public override string ToString() => Shown ? $"{Name} {Count}" : "(hidden)";
    }
}
