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
            // One-handed swords, lying flat: any heading, following the ground. Held in either hand, so two make a pair,
            // and swung in the One-Handed style, each blow taking its damage from health. The Viking Sword is from the
            // blender-tests art repo, the others from OpenGameArt.
            foreach (var (id, name, folder, weight, value, damage) in new[]
                     {
                         (VikingSword(pluginId), "Viking Sword", "VikingSword", 9f, 75, 12f),
                         (new RecordId(pluginId, "short_sword"), "Short Sword", "ShortSword", 6f, 40, 9f),
                         (new RecordId(pluginId, "arming_sword"), "Arming Sword", "ArmingSword", 8f, 70, 11f),
                     })
                yield return new BaseObjectRecord(id, name, ModelImport.KeyFor(folder), "Items/Weapons", LyingFlat(),
                    item: new ItemPart(ItemKind.Weapon, weight, value),
                    equippable: new EquippablePart(CoreEquipSlots.OneHanded()),
                    weapon: new WeaponPart(CoreCombatStyles.OneHanded, new[] { Effect.Damage(CoreAttributes.Health, damage) }));

            // A heavy two-handed sword (OpenGameArt, CC0), lying flat as the swords do. No combat style yet: the base
            // game has none for two hands.
            yield return new BaseObjectRecord(Greatsword(pluginId), "Greatsword", ModelImport.KeyFor("Greatsword"),
                "Items/Weapons", LyingFlat(),
                item: new ItemPart(ItemKind.Weapon, weight: 18f, value: 120),
                equippable: new EquippablePart(CoreEquipSlots.TwoHanded()));

            // A mage's staff (OpenGameArt, CC0), lying flat; held in one hand, the other free. No combat style yet.
            yield return new BaseObjectRecord(new RecordId(pluginId, "mage_staff"), "Mage Staff", ModelImport.KeyFor("MageStaff"),
                "Items/Weapons", LyingFlat(),
                item: new ItemPart(ItemKind.Weapon, weight: 6f, value: 90),
                equippable: new EquippablePart(CoreEquipSlots.OneHanded()));

            // Bows (OpenGameArt), lying flat; they take both hands, held in the left, and are drawn from the back in the
            // Archery style. An arrow's hit is the bow's damage to health (no shot yet).
            foreach (var (folder, id, name, weight, value, damage) in new[]
                     {
                         ("HuntingBow", "hunting_bow", "Hunting Bow", 3f, 50, 7f),
                         ("RecurveBow", "recurve_bow", "Recurve Bow", 3f, 65, 9f),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder),
                    "Items/Weapons", LyingFlat(),
                    item: new ItemPart(ItemKind.Weapon, weight, value),
                    equippable: new EquippablePart(CoreEquipSlots.Bow()),
                    weapon: new WeaponPart(CoreCombatStyles.Archery, new[] { Effect.Damage(CoreAttributes.Health, damage) }));

            // Arrows and a quiver of them (OpenGameArt, CC0), lying flat; carried, not held, until there is shooting.
            foreach (var (folder, id, name, weight, value) in new[]
                     {
                         ("Arrow", "arrow", "Arrow", 0.1f, 1),
                         ("BroadheadArrow", "broadhead_arrow", "Broadhead Arrow", 0.1f, 2),
                         ("Quiver", "quiver", "Quiver of Arrows", 2f, 20),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder),
                    "Items/Ammo", LyingFlat(),
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
                    "Items/Shields", LyingFlat(),
                    item: new ItemPart(ItemKind.Weapon, weight, value),
                    equippable: new EquippablePart(CoreEquipSlots.OffHand()));

            // Houses (OpenGameArt): upright, placed as aimed, at their own size.
            foreach (var (folder, id, name) in new[]
                     {
                         ("MedievalCottage", "medieval_cottage", "Medieval Cottage"),
                         ("TimberHouse", "timber_house", "Timber-Framed House"),
                         ("OldShack", "old_shack", "Thatched Shack"),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder),
                    "Architecture/Buildings", new PlacementRules(randomYaw: false, 1f, 1f, alignToSlope: false));

            // Trees and plants (OpenGameArt), cut out of their leaves' alpha: upright, any heading, a size about their own.
            foreach (var (folder, id, name, category, minScale, maxScale, alignToSlope) in new[]
                     {
                         ("BalsamFir", "balsam_fir", "Balsam Fir", "Nature/Trees", 0.8f, 1.3f, false),
                         ("BeechTree", "beech_tree", "Beech Tree", "Nature/Trees", 0.8f, 1.3f, false),
                         ("Bush1", "bush_leafy", "Leafy Bush", "Nature/Plants", 0.7f, 1.3f, true),
                         ("Bush2", "bush_autumn", "Autumn Bush", "Nature/Plants", 0.7f, 1.3f, true),
                         ("Bush3", "bush_wild", "Wild Shrub", "Nature/Plants", 0.7f, 1.3f, true),
                         ("Bush4", "bush_grassy", "Grassy Bush", "Nature/Plants", 0.7f, 1.3f, true),
                         ("Bush5", "bush_broadleaf", "Broadleaf Bush", "Nature/Plants", 0.7f, 1.3f, true),
                         ("FloweringBush", "flowering_bush", "Flowering Bush", "Nature/Plants", 0.7f, 1.3f, true),
                         ("LadyFern", "lady_fern", "Lady Fern", "Nature/Plants", 0.7f, 1.3f, true),
                         ("Burdock", "burdock", "Burdock", "Nature/Plants", 0.7f, 1.3f, true),
                         ("Cattail", "cattail", "Cattails", "Nature/Plants", 0.8f, 1.2f, true),
                         ("OxeyeDaisy", "oxeye_daisy", "Oxeye Daisy", "Nature/Flowers", 0.8f, 1.2f, true),
                         ("WildCarrot", "wild_carrot", "Wild Carrot", "Nature/Flowers", 0.8f, 1.2f, true),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder), category,
                    new PlacementRules(randomYaw: true, minScale, maxScale, alignToSlope));
        }

        // Laid on the ground as it falls: any heading, following the slope, at its own size.
        static PlacementRules LyingFlat() => new PlacementRules(randomYaw: true, 1f, 1f, alignToSlope: true);
    }
}
