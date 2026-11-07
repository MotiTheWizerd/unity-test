using System.Collections.Generic;
using Forge.Data.Records;
using Forge.Game.Equipment;

namespace Forge.UnityEditor.Content
{
    /// <summary>The equip slots the base game ships (<see cref="CoreEquipSlots"/>): the two hands, held at the humanoid hand bones.</summary>
    static class CoreEquipSlotSet
    {
        public static IEnumerable<EquipSlotRecord> All()
        {
            yield return new EquipSlotRecord(CoreEquipSlots.RightHand, "Right Hand", "RightHand");
            yield return new EquipSlotRecord(CoreEquipSlots.LeftHand, "Left Hand", "LeftHand");
        }
    }
}
