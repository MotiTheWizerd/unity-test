namespace Forge.Data.Records
{
    /// <summary>
    /// Where an <see cref="ItemPart"/>'s item is listed in an inventory, as Skyrim's tabs. Grouping only: what an item
    /// does (wielded, worn, drunk) comes from its parts, never from its kind. Written by name, so more kinds (apparel with
    /// equipment, books, keys) are added without touching a file.
    /// </summary>
    public enum ItemKind
    {
        /// <summary>Everything else: gold, tools, trinkets. An item that names no kind is one.</summary>
        Misc,

        /// <summary>Swords, axes, bows.</summary>
        Weapon,

        /// <summary>Drinks that do something: healing, stamina.</summary>
        Potion,

        /// <summary>Things to eat.</summary>
        Food,
    }
}
