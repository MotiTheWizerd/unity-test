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

        /// <summary>The Hunting Bow's id in <paramref name="pluginId"/>.</summary>
        public static RecordId HuntingBow(string pluginId) => new RecordId(pluginId, "hunting_bow");

        /// <summary>The (plain) Arrow's id in <paramref name="pluginId"/>.</summary>
        public static RecordId Arrow(string pluginId) => new RecordId(pluginId, "arrow");

        /// <summary>The Greatsword's id in <paramref name="pluginId"/>.</summary>
        public static RecordId Greatsword(string pluginId) => new RecordId(pluginId, "greatsword");

        /// <summary>The Painted Round Shield's id in <paramref name="pluginId"/>.</summary>
        public static RecordId PaintedRoundShield(string pluginId) => new RecordId(pluginId, PaintedRoundShieldKey);

        const string PaintedRoundShieldKey = "round_shield_painted";

        // A shield bash's damage, and the stamina every shield's block costs per point of damage it stops.
        const float ShieldBash = 3f;
        const float BlockEffort = 0.5f;

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
            // Archery style, shooting the arrows equipped as ammo. An arrow's hit is the bow's damage to health, times how far it was drawn.
            foreach (var (folder, id, name, weight, value, damage) in new[]
                     {
                         ("HuntingBow", HuntingBow(pluginId), "Hunting Bow", 3f, 50, 7f),
                         ("RecurveBow", new RecordId(pluginId, "recurve_bow"), "Recurve Bow", 3f, 65, 9f),
                     })
                yield return new BaseObjectRecord(id, name, ModelImport.KeyFor(folder),
                    "Items/Weapons", LyingFlat(),
                    item: new ItemPart(ItemKind.Weapon, weight, value),
                    equippable: new EquippablePart(CoreEquipSlots.Bow()),
                    weapon: new WeaponPart(CoreCombatStyles.Archery, new[] { Effect.Damage(CoreAttributes.Health, damage) }, CoreEquipSlots.Ammo));

            // Arrows (OpenGameArt, CC0), lying flat: equipped as ammo, worn as a quiver of them on the back, as in Skyrim.
            // One in five breaks where it lands; the rest can be taken back.
            foreach (var (folder, id, name, weight, value) in new[]
                     {
                         ("Arrow", Arrow(pluginId), "Arrow", 0.1f, 1),
                         ("BroadheadArrow", new RecordId(pluginId, "broadhead_arrow"), "Broadhead Arrow", 0.1f, 2),
                     })
                yield return new BaseObjectRecord(id, name, ModelImport.KeyFor(folder),
                    "Items/Ammo", LyingFlat(),
                    item: new ItemPart(ItemKind.Misc, weight, value),
                    equippable: new EquippablePart(CoreEquipSlots.Ammunition(), worn: ModelImport.KeyFor("Quiver")),
                    ammo: new AmmoPart(breakChance: 0.2f));

            // A quiver of arrows (OpenGameArt, CC0), lying flat; carried, its look the one arrows are worn by.
            yield return new BaseObjectRecord(new RecordId(pluginId, "quiver"), "Quiver of Arrows", ModelImport.KeyFor("Quiver"),
                "Items/Ammo", LyingFlat(),
                item: new ItemPart(ItemKind.Misc, 2f, 20));

            // Shields (OpenGameArt, CC0), two looks of each shape, lying face up; held in the off hand and raised against
            // blows (the Shield style), stopping more of one the sturdier they are, a bash a light blow.
            foreach (var (folder, id, name, weight, value, stops) in new[]
                     {
                         ("RoundShield", "round_shield", "Round Shield", 12f, 30, 0.5f),
                         ("RoundShieldPainted", PaintedRoundShieldKey, "Painted Round Shield", 12f, 40, 0.5f),
                         ("KiteShield", "kite_shield", "Kite Shield", 14f, 45, 0.6f),
                         ("KiteShieldIron", "kite_shield_iron", "Iron Kite Shield", 18f, 80, 0.75f),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder),
                    "Items/Shields", LyingFlat(),
                    item: new ItemPart(ItemKind.Weapon, weight, value),
                    equippable: new EquippablePart(CoreEquipSlots.OffHand()),
                    weapon: new WeaponPart(CoreCombatStyles.Shield, new[] { Effect.Damage(CoreAttributes.Health, ShieldBash) }),
                    block: new BlockPart(stops, BlockEffort));

            // Houses (OpenGameArt): upright, placed as aimed, at their own size.
            foreach (var (folder, id, name) in new[]
                     {
                         ("MedievalCottage", "medieval_cottage", "Medieval Cottage"),
                         ("TimberHouse", "timber_house", "Timber-Framed House"),
                         ("OldShack", "old_shack", "Thatched Shack"),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder),
                    "Architecture/Buildings", new PlacementRules(randomYaw: false, 1f, 1f, alignToSlope: false));

            // Ships (Poly Haven, CC0), bow to the front: upright, placed as aimed, at their own size, their keel on the
            // pivot. The two large ones are one ship moored (sails furled) and under sail.
            foreach (var (folder, id, name) in new[]
                     {
                         ("DutchShipMedium", "dutch_ship_medium", "Dutch Merchant Ship"),
                         ("DutchShipLarge", "dutch_ship_large", "Dutch Galleon, Moored"),
                         ("DutchShipLargeSailing", "dutch_ship_large_sailing", "Dutch Galleon, Under Sail"),
                         ("Pinnace", "pinnace", "Pinnace"),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder),
                    "Vessels/Ships", new PlacementRules(randomYaw: false, 1f, 1f, alignToSlope: false));

            // A weathered wooden pier (Poly Haven, CC0) in sections that join end to end, and a hoist frame of two posts:
            // placed as aimed, at their own size, the posts standing in the water below the deck.
            foreach (var (n, name) in new[]
                     {
                         (1, "Pier Section, Rope Rail"),
                         (2, "Pier Section"),
                         (3, "Pier Section, Braced"),
                         (4, "Pier Section, Log Deck"),
                         (5, "Pier Hoist Frame"),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, $"pier_section_{n}"), name, ModelImport.KeyFor($"PierSection{n}"),
                    "Architecture/Docks", new PlacementRules(randomYaw: false, 1f, 1f, alignToSlope: false));

            // Harbor and storehouse props (Poly Haven, CC0): upright, any heading, about their own size.
            foreach (var (folder, id, name, category) in new[]
                     {
                         ("SmallCrate", "small_crate", "Small Wooden Crate", "Props/Containers"),
                         ("LongCrate", "long_crate", "Long Wooden Crate", "Props/Containers"),
                         ("WoodenBarrel", "wooden_barrel", "Wooden Barrel", "Props/Containers"),
                         ("WornBarrel", "worn_barrel", "Worn Wooden Barrel", "Props/Containers"),
                         ("WineBarrel", "wine_barrel", "Wine Barrel", "Props/Containers"),
                         ("WoodenBucket", "wooden_bucket", "Wooden Bucket", "Props/Containers"),
                         ("WashTub", "wash_tub", "Wooden Wash Tub", "Props/Containers"),
                         ("WickerTray", "wicker_tray", "Wicker Tray", "Props/Containers"),
                         ("LiddedBasket", "lidded_basket", "Lidded Wicker Basket", "Props/Containers"),
                         ("TreasureChest", "treasure_chest", "Treasure Chest", "Props/Containers"),
                         ("WoodenLantern", "wooden_lantern", "Wooden Lantern", "Props/Lighting"),
                         ("Cannon", "cannon", "Ship's Cannon", "Props/Military"),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder), category,
                    new PlacementRules(randomYaw: true, 0.9f, 1.1f, alignToSlope: false));

            // A town (OpenGameArt, CC0: system-g6's medieval packs, iconoclast's well, hreikin's houses). Buildings, gates
            // and the market: upright, placed as aimed, at their own size. Fences and the palisade follow the ground, so
            // a place_line run of them climbs a slope.
            foreach (var (folder, id, name, category, followsGround) in new[]
                     {
                         ("StoneCottage", "stone_cottage", "Stone Cottage", "Architecture/Buildings", false),
                         ("PlasterHouse", "plaster_house", "Plastered Town House", "Architecture/Buildings", false),
                         ("BrickHouse", "brick_house", "Brick Town House", "Architecture/Buildings", false),
                         ("WoodenHut", "wooden_hut", "Wooden Hut", "Architecture/Buildings", false),
                         ("Longhouse", "longhouse", "Longhouse", "Architecture/Buildings", false),
                         ("Outhouse", "outhouse", "Outhouse", "Architecture/Buildings", false),
                         ("Watchtower", "watchtower", "Wooden Watchtower", "Architecture/Buildings", false),
                         ("MarketStall", "market_stall", "Market Stall", "Architecture/Town", false),
                         ("WoodenAwning", "wooden_awning", "Wooden Awning", "Architecture/Town", false),
                         ("CoveredWell", "covered_well", "Covered Well", "Architecture/Town", false),
                         ("TownGate", "town_gate", "Wooden Town Gate", "Architecture/Gates", false),
                         ("IronGate", "iron_gate", "Iron Gate", "Architecture/Gates", false),
                         ("StoneArchway", "stone_archway", "Stone Archway", "Architecture/Gates", false),
                         ("Palisade", "palisade", "Wooden Palisade", "Architecture/Walls", true),
                         ("Fence", "fence", "Wooden Fence", "Architecture/Walls", true),
                         ("FenceGate", "fence_gate", "Fence Gate", "Architecture/Walls", true),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder), category,
                    new PlacementRules(randomYaw: false, 1f, 1f, alignToSlope: followsGround));

            // The street's dressing (system-g6's props, CC0): upright, any heading, about their own size; logs lie on
            // the ground as it slopes.
            foreach (var (folder, id, name, category, followsGround) in new[]
                     {
                         ("Cart", "cart", "Hand Cart", "Props/Street", false),
                         ("HayBundle", "hay_bundle", "Hay Bundle", "Props/Street", false),
                         ("WaterTrough", "water_trough", "Water Trough", "Props/Street", false),
                         ("HitchingPost", "hitching_post", "Hitching Post", "Props/Street", false),
                         ("Sawbuck", "sawbuck", "Sawbuck", "Props/Street", false),
                         ("Log", "log", "Log", "Props/Street", true),
                         ("Firewood", "chopping_block", "Chopping Block", "Props/Street", false),
                         ("Bench", "wooden_bench", "Wooden Bench", "Props/Furniture", false),
                         ("WoodenChest", "wooden_chest", "Wooden Chest", "Props/Containers", false),
                         ("Campfire", "log_campfire", "Log Campfire", "Props/Lighting", false),
                         ("StoneBrazier", "stone_brazier", "Stone Brazier", "Props/Lighting", false),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder), category,
                    new PlacementRules(randomYaw: true, 0.9f, 1.1f, alignToSlope: followsGround));

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

            // Ground clutter (Poly Haven, CC0): the small things that make ground look lived in, scattered by the
            // hundred along street edges, walls and paths and under trees. Any heading, a size about their own;
            // stones, rocks, branches and roots lie with the slope, the stump stands up.
            foreach (var (folder, id, name, category, minScale, maxScale, alignToSlope) in new[]
                     {
                         ("Stone", "small_stone", "Small Stone", "Nature/Rocks", 0.7f, 1.3f, true),
                         ("MossyRock", "large_mossy_rock", "Large Mossy Rock", "Nature/Rocks", 0.7f, 1.3f, true),
                         ("MossyRockLow", "mossy_rock_low", "Low Mossy Rock", "Nature/Rocks", 0.7f, 1.3f, true),
                         ("MossyBoulder", "mossy_rock", "Mossy Rock", "Nature/Rocks", 0.8f, 1.2f, true),
                         ("DryBranches", "dry_branches", "Fallen Branches", "Nature/Ground Cover", 0.8f, 1.2f, true),
                         ("Roots", "roots", "Roots", "Nature/Ground Cover", 0.8f, 1.2f, true),
                         ("TreeStump", "tree_stump", "Tree Stump", "Nature/Ground Cover", 0.8f, 1.2f, false),
                         ("Fern", "fern", "Fern", "Nature/Plants", 0.7f, 1.3f, true),
                         ("Weed", "weed", "Weeds", "Nature/Ground Cover", 0.7f, 1.3f, true),
                         ("Dandelion", "dandelion", "Dandelion", "Nature/Flowers", 0.8f, 1.2f, true),
                     })
                yield return new BaseObjectRecord(new RecordId(pluginId, id), name, ModelImport.KeyFor(folder), category,
                    new PlacementRules(randomYaw: true, minScale, maxScale, alignToSlope));

            // Creatures (Tripo AI from Moti's own picture, made game-ready by blender-tests/creatures): standing still,
            // as Forge has no moving creatures yet; upright, placed as aimed, at their own size.
            yield return new BaseObjectRecord(new RecordId(pluginId, "dragon"), "Dragon", ModelImport.KeyFor("Dragon"),
                "Creatures/Dragons", new PlacementRules(randomYaw: false, 1f, 1f, alignToSlope: false));
        }

        // Laid on the ground as it falls: any heading, following the slope, at its own size.
        static PlacementRules LyingFlat() => new PlacementRules(randomYaw: true, 1f, 1f, alignToSlope: true);
    }
}
