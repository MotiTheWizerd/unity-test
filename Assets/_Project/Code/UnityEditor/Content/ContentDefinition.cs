using System;
using Forge.Data.Records;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// One placeable object: the base-object record it becomes in forge.core and how its
    /// placeholder prefab is built. The single source for both, so they can't drift apart.
    /// </summary>
    sealed class ContentDefinition
    {
        public ContentDefinition(string name, string displayName, string category, string assetKey, Action<PrefabBuilder> build, PlacementRules placement = null,
            ItemPart item = null)
        {
            Name = name;
            DisplayName = displayName;
            Category = category;
            AssetKey = assetKey;
            Build = build;
            Placement = placement;
            Item = item;
        }

        /// <summary>How new instances are placed: nature varies, ground-hugging things follow the slope, the rest stand upright.</summary>
        public PlacementRules Placement { get; }

        /// <summary>What makes it an item to carry (a potion, gold); null for the rest of the world.</summary>
        public ItemPart Item { get; }

        /// <summary>Record name inside forge.core, e.g. "tree_pine".</summary>
        public string Name { get; }

        public string DisplayName { get; }

        public string Category { get; }

        /// <summary>Key the record stores and the prefab catalog resolves, e.g. "nature/tree_pine".</summary>
        public string AssetKey { get; }

        /// <summary>Adds the prefab's parts. The pivot is the ground contact point, so objects sit on what they're placed on.</summary>
        public Action<PrefabBuilder> Build { get; }
    }
}
