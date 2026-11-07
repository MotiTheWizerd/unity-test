using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Attributes;
using Forge.Game.Combat;
using Forge.Game.Equipment;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The base game's objects with real art: a model folder under Content/Models each (<see cref="ModelBuilder"/>), as
    /// base-object records whose asset keys are their models' (<see cref="ModelImport.KeyFor"/>).
    /// </summary>
    static class CoreModels
    {
        /// <summary>The Viking Sword's id in <paramref name="pluginId"/>.</summary>
        public static RecordId VikingSword(string pluginId) => new RecordId(pluginId, "viking_sword");

        /// <summary>The Greatsword's id in <paramref name="pluginId"/>.</summary>
        public static RecordId Greatsword(string pluginId) => new RecordId(pluginId, "greatsword");

        public static IEnumerable<BaseObjectRecord> All(string pluginId)
        {
            // A one-handed sword from the blender-tests art repo, lying flat: any heading, following the ground. Held in
            // either hand, so two make a pair.
            yield return new BaseObjectRecord(VikingSword(pluginId), "Viking Sword", ModelImport.KeyFor("VikingSword"),
                "Items/Weapons", new PlacementRules(randomYaw: true, 1f, 1f, alignToSlope: true),
                item: new ItemPart(ItemKind.Weapon, weight: 9f, value: 75),
                equippable: new EquippablePart(CoreEquipSlots.OneHanded()),
                weapon: new WeaponPart(CoreCombatStyles.OneHanded, new[] { Effect.Damage(CoreAttributes.Health, 12f) }));

            // A heavy two-handed sword (OpenGameArt, CC0), lying flat as the Viking Sword does. No combat style yet: the
            // base game has none for two hands.
            yield return new BaseObjectRecord(Greatsword(pluginId), "Greatsword", ModelImport.KeyFor("Greatsword"),
                "Items/Weapons", new PlacementRules(randomYaw: true, 1f, 1f, alignToSlope: true),
                item: new ItemPart(ItemKind.Weapon, weight: 18f, value: 120),
                equippable: new EquippablePart(CoreEquipSlots.TwoHanded()));

            // A mage's staff (OpenGameArt, CC0), lying flat; held in one hand, the other free. No combat style yet.
            yield return new BaseObjectRecord(new RecordId(pluginId, "mage_staff"), "Mage Staff", ModelImport.KeyFor("MageStaff"),
                "Items/Weapons", new PlacementRules(randomYaw: true, 1f, 1f, alignToSlope: true),
                item: new ItemPart(ItemKind.Weapon, weight: 6f, value: 90),
                equippable: new EquippablePart(CoreEquipSlots.OneHanded()));

            // One-handed swords and bows (OpenGameArt), lying flat. No combat style yet; bows take both hands to draw.
            foreach (var (folder, id, name, weight, value, fits) in new[]
                     {
                         ("ShortSword", "short_sword", "Short Sword", 6f, 40, CoreEquipSlots.OneHanded()),
                         ("ArmingSword", "arming_sword", "Arming Sword", 8f, 70, CoreEquipSlots.OneHanded()),
                         ("HuntingBow", "hunting_bow", "Hunting Bow", 3f, 50, CoreEquipSlots.TwoHanded()),
                         ("RecurveBow", "recurve_bow", "Recurve Bow", 3f, 65, CoreEquipSlots.TwoHanded()),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder),
                    "Items/Weapons", new PlacementRules(randomYaw: true, 1f, 1f, alignToSlope: true),
                    item: new ItemPart(ItemKind.Weapon, weight, value),
                    equippable: new EquippablePart(fits));

            // Arrows and a quiver of them (OpenGameArt, CC0), lying flat; carried, not held, until there is shooting.
            foreach (var (folder, id, name, weight, value) in new[]
                     {
                         ("Arrow", "arrow", "Arrow", 0.1f, 1),
                         ("BroadheadArrow", "broadhead_arrow", "Broadhead Arrow", 0.1f, 2),
                         ("Quiver", "quiver", "Quiver of Arrows", 2f, 20),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder),
                    "Items/Ammo", new PlacementRules(randomYaw: true, 1f, 1f, alignToSlope: true),
                    item: new ItemPart(ItemKind.Misc, weight, value));

            // Shields (OpenGameArt, CC0), two looks of each shape, lying face up; held in the off hand.
            foreach (var (folder, id, name, weight, value) in new[]
                     {
                         ("RoundShield", "round_shield", "Round Shield", 12f, 30),
                         ("RoundShieldPainted", "round_shield_painted", "Painted Round Shield", 12f, 40),
                         ("KiteShield", "kite_shield", "Kite Shield", 14f, 45),
                         ("KiteShieldIron", "kite_shield_iron", "Iron Kite Shield", 18f, 80),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder),
                    "Items/Shields", new PlacementRules(randomYaw: true, 1f, 1f, alignToSlope: true),
                    item: new ItemPart(ItemKind.Weapon, weight, value),
                    equippable: new EquippablePart(CoreEquipSlots.OffHand()));
        }
    }
}
