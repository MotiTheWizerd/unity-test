using System;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// One placeable object: the base-object record it becomes in forge.core and how its
    /// placeholder prefab is built. The single source for both, so they can't drift apart.
    /// </summary>
    sealed class ContentDefinition
    {
        public ContentDefinition(string name, string displayName, string category, string assetKey, Action<PrefabBuilder> build)
        {
            Name = name;
            DisplayName = displayName;
            Category = category;
            AssetKey = assetKey;
            Build = build;
        }

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
