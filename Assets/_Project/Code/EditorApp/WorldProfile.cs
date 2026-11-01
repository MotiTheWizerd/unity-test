using System.Collections.Generic;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// Which world the editor opens: the base plugin folders (under StreamingAssets,
    /// loaded as read-only masters) and the plugin that receives the edits. Separate
    /// worlds keep separate edit plugins, so one world's edits never depend on
    /// another world's masters.
    /// </summary>
    [CreateAssetMenu(menuName = "Forge/World Profile", fileName = "WorldProfile")]
    public sealed class WorldProfile : ScriptableObject
    {
        [SerializeField, Tooltip("Folders under StreamingAssets whose plugins are the read-only masters.")]
        string[] _baseFolders = { "Plugins" };

        [SerializeField, Tooltip("Plugin that receives all edits; saved under the user's data folder.")]
        string _activePluginId = "forge.user";

        public IReadOnlyList<string> BaseFolders => _baseFolders;

        public string ActivePluginId => _activePluginId;
    }
}
