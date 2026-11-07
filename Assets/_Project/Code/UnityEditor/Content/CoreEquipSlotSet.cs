using System.Collections.Generic;
using Forge.Data.Records;
using Forge.Game.Equipment;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The equip slots the base game ships (<see cref="CoreEquipSlots"/>): the two hands, held at the humanoid hand bones,
    /// what they hold sheathed until readied.
    /// </summary>
    static class CoreEquipSlotSet
    {
        public static IEnumerable<EquipSlotRecord> All()
        {
            yield return new EquipSlotRecord(CoreEquipSlots.RightHand, "Right Hand", "RightHand", sheathes: true);
            yield return new EquipSlotRecord(CoreEquipSlots.LeftHand, "Left Hand", "LeftHand", sheathes: true);
        }
    }
}
