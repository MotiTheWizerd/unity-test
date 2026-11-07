using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Equipment;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The base game's objects with real art: a model folder under Content/Models each (<see cref="ModelBuilder"/>), as
    /// base-object records whose asset keys are their models' (<see cref="ModelImport.KeyFor"/>).
    /// </summary>
    static class CoreModels
    {
        public static IEnumerable<BaseObjectRecord> All(string pluginId)
        {
            // A one-handed sword from the blender-tests art repo, lying flat: any heading, following the ground. Held in
            // either hand, so two make a pair.
            yield return new BaseObjectRecord(new RecordId(pluginId, "viking_sword"), "Viking Sword", ModelImport.KeyFor("VikingSword"),
                "Items/Weapons", new PlacementRules(randomYaw: true, 1f, 1f, alignToSlope: true),
                item: new ItemPart(ItemKind.Weapon, weight: 9f, value: 75),
                equippable: new EquippablePart(CoreEquipSlots.OneHanded()));
        }
    }
}
