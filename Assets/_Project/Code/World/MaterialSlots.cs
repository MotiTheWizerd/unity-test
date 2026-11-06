using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using UnityEngine;

namespace Forge.World
{
    /// <summary>
    /// The material slots of a presented object and the materials its asset wears in them by default, captured once when
    /// it is built, so a slot can be dressed and given back its base look any number of times. A slot is named after its
    /// default material ("Roof"), which may be worn by several parts and renderers: they are all one slot
    /// (<see cref="MaterialOverride"/>).
    /// </summary>
    public sealed class MaterialSlots : MonoBehaviour
    {
        [NonSerialized] Renderer[] _renderers = Array.Empty<Renderer>();
        [NonSerialized] Material[][] _defaults = Array.Empty<Material[]>();
        [NonSerialized] string[] _slots = Array.Empty<string>();

        /// <summary>The slot names, in the order the asset's renderers first wear them.</summary>
        public IReadOnlyList<string> Slots => _slots;

        /// <summary>Adds the component to <paramref name="target"/>, capturing what its renderers wear now as their defaults.</summary>
        public static MaterialSlots Capture(GameObject target)
        {
            var slots = target.AddComponent<MaterialSlots>();
            slots._renderers = target.GetComponentsInChildren<Renderer>(true);
            slots._defaults = new Material[slots._renderers.Length][];
            var names = new List<string>();
            for (var i = 0; i < slots._renderers.Length; i++)
            {
                slots._defaults[i] = slots._renderers[i].sharedMaterials;
                foreach (var material in slots._defaults[i])
                {
                    var name = SlotOf(material);
                    if (name != null && !names.Contains(name))
                        names.Add(name);
                }
            }

            slots._slots = names.ToArray();
            return slots;
        }

        /// <summary>The slot <paramref name="material"/> stands for: its name, cut to a slot name's length; null for none.</summary>
        public static string SlotOf(Material material)
        {
            if (material == null || string.IsNullOrWhiteSpace(material.name))
                return null;
            var name = material.name.Trim();
            return name.Length > MaterialOverride.MaxSlotLength ? name.Substring(0, MaterialOverride.MaxSlotLength) : name;
        }

        /// <summary>
        /// Dresses every slot <paramref name="overrides"/> names in the material <paramref name="look"/> gives for it, and
        /// every other slot (or one whose look is null) in its default.
        /// </summary>
        public void Dress(IReadOnlyList<MaterialOverride> overrides, Func<RecordId, Material> look)
        {
            Dictionary<string, Material> dressed = null;
            foreach (var material in overrides)
            {
                var worn = look(material.Material);
                if (worn != null)
                    (dressed ??= new Dictionary<string, Material>(StringComparer.Ordinal))[material.Slot] = worn;
            }

            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                    continue;
                var defaults = _defaults[i];
                var materials = new Material[defaults.Length];
                for (var m = 0; m < defaults.Length; m++)
                {
                    var slot = SlotOf(defaults[m]);
                    materials[m] = slot != null && dressed != null && dressed.TryGetValue(slot, out var worn) ? worn : defaults[m];
                }

                _renderers[i].sharedMaterials = materials;
            }
        }
    }
}
