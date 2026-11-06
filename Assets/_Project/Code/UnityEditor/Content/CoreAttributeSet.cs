using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Attributes;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The attributes the base game ships (<see cref="CoreAttributes"/>), as Skyrim tunes them: a hundred each of health,
    /// stamina and magicka, stamina quick to come back and health slow; carry weight 300, half a pound more per point of
    /// stamina; ten seconds of sprint on a full bar; a fall past four meters hurts.
    /// </summary>
    static class CoreAttributeSet
    {
        public static IEnumerable<AttributeRecord> All()
        {
            foreach (var record in Resource(CoreAttributes.Health, "Health", 100f, CoreAttributes.HealthRegen, 1f, CoreAttributes.HealthRegenDelay, 5f))
                yield return record;
            foreach (var record in Resource(CoreAttributes.Stamina, "Stamina", 100f, CoreAttributes.StaminaRegen, 10f, CoreAttributes.StaminaRegenDelay, 1.5f))
                yield return record;
            foreach (var record in Resource(CoreAttributes.Magicka, "Magicka", 100f, CoreAttributes.MagickaRegen, 3f, CoreAttributes.MagickaRegenDelay, 2f))
                yield return record;

            yield return new AttributeRecord(CoreAttributes.CarryWeight, "Carry Weight", AttributeKind.Stat, 250f,
                terms: new[] { new AttributeTerm(CoreAttributes.Stamina, 0.5f) });
            yield return Stat(CoreAttributes.SprintCost, "Sprint Cost", 10f);
            yield return Stat(CoreAttributes.JumpCost, "Jump Cost", 10f);
            yield return Stat(CoreAttributes.FallSafeHeight, "Safe Fall Height", 4f);
            yield return Stat(CoreAttributes.FallDamage, "Fall Damage", 10f);
        }

        // A resource and the two stats it refills by: points a second, and seconds waited after being spent.
        static IEnumerable<AttributeRecord> Resource(RecordId id, string name, float most, RecordId regen, float perSecond, RecordId delay, float seconds)
        {
            yield return new AttributeRecord(id, name, AttributeKind.Resource, most, regenRate: regen, regenDelay: delay);
            yield return Stat(regen, name + " Regen", perSecond);
            yield return Stat(delay, name + " Regen Delay", seconds);
        }

        static AttributeRecord Stat(RecordId id, string name, float value) => new AttributeRecord(id, name, AttributeKind.Stat, value);
    }
}
