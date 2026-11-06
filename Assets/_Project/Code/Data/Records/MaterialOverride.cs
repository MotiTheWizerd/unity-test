using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// One material slot of a placed object dressed in another material than its base object gives it: the slot, by
    /// name, and the <see cref="MaterialRecord"/> worn there instead. A slot is named after the material the object's
    /// asset wears there by default ("Roof", "Plaster"), as a mesh's material slots are named in Blender or Unreal, so
    /// the data never depends on how the asset is put together inside.
    /// </summary>
    public readonly struct MaterialOverride : IEquatable<MaterialOverride>
    {
        /// <summary>Longest slot name allowed.</summary>
        public const int MaxSlotLength = 64;

        public MaterialOverride(string slot, RecordId material)
        {
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("A slot name is required.", nameof(slot));
            if (slot.Length > MaxSlotLength)
                throw new ArgumentException($"Slot names are at most {MaxSlotLength} characters, not {slot.Length}.", nameof(slot));
            foreach (var c in slot)
            {
                if (char.IsControl(c))
                    throw new ArgumentException("Slot names can't hold control characters.", nameof(slot));
            }

            if (material.IsDefault)
                throw new ArgumentException("A material is required.", nameof(material));

            Slot = slot;
            Material = material;
        }

        /// <summary>The slot, named after the material the asset wears there by default.</summary>
        public string Slot { get; }

        /// <summary>The material worn there instead.</summary>
        public RecordId Material { get; }

        public bool Equals(MaterialOverride other) => string.Equals(Slot, other.Slot, StringComparison.Ordinal) && Material == other.Material;

        public override bool Equals(object obj) => obj is MaterialOverride other && Equals(other);

        public override int GetHashCode() => ((Slot != null ? StringComparer.Ordinal.GetHashCode(Slot) : 0) * 397) ^ Material.GetHashCode();

        public static bool operator ==(MaterialOverride a, MaterialOverride b) => a.Equals(b);

        public static bool operator !=(MaterialOverride a, MaterialOverride b) => !a.Equals(b);

        public override string ToString() => $"{Slot} → {Material}";
    }
}
