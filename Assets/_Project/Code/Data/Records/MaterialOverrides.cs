using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Records
{
    /// <summary>
    /// The rules of a list of <see cref="MaterialOverride"/>s, shared by base objects (a variant's look) and placed
    /// objects (one instance's look): sorted by slot, each slot once, at most <see cref="Max"/>; and how the two layer.
    /// </summary>
    public static class MaterialOverrides
    {
        /// <summary>Most slots one list can dress.</summary>
        public const int Max = 64;

        public static readonly IReadOnlyList<MaterialOverride> None = Array.Empty<MaterialOverride>();

        /// <summary><paramref name="materials"/> sorted by slot and checked; null or empty gives <see cref="None"/>.</summary>
        /// <exception cref="ArgumentException">A slot is missing or dressed twice, or there are too many.</exception>
        public static IReadOnlyList<MaterialOverride> Validated(IEnumerable<MaterialOverride> materials, string paramName)
        {
            if (materials == null)
                return None;

            var sorted = materials.OrderBy(m => m.Slot, StringComparer.Ordinal).ToArray();
            if (sorted.Length == 0)
                return None;
            if (sorted.Length > Max)
                throw new ArgumentException($"At most {Max} slots can be dressed, not {sorted.Length}.", paramName);
            for (var i = 0; i < sorted.Length; i++)
            {
                if (sorted[i].Slot == null)
                    throw new ArgumentException("A material override needs a slot.", paramName);
                if (i > 0 && sorted[i - 1].Slot == sorted[i].Slot)
                    throw new ArgumentException($"Slot '{sorted[i].Slot}' is dressed more than once.", paramName);
            }

            return sorted;
        }

        /// <summary>
        /// What an object wears: <paramref name="under"/> (its base object's) with <paramref name="over"/> (its own) on
        /// top, slot by slot. Either may be null.
        /// </summary>
        public static IReadOnlyList<MaterialOverride> Layer(IReadOnlyList<MaterialOverride> under, IReadOnlyList<MaterialOverride> over)
        {
            if (under == null || under.Count == 0)
                return over ?? None;
            if (over == null || over.Count == 0)
                return under;

            var bySlot = new SortedDictionary<string, MaterialOverride>(StringComparer.Ordinal);
            foreach (var material in under)
                bySlot[material.Slot] = material;
            foreach (var material in over)
                bySlot[material.Slot] = material;
            return bySlot.Values.ToArray();
        }

        /// <summary>The material <paramref name="materials"/> puts in <paramref name="slot"/>, or null.</summary>
        public static RecordId? In(IReadOnlyList<MaterialOverride> materials, string slot)
        {
            if (materials == null)
                return null;
            foreach (var material in materials)
            {
                if (string.Equals(material.Slot, slot, StringComparison.Ordinal))
                    return material.Material;
            }

            return null;
        }
    }
}
