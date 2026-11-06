using System.Collections.Generic;

namespace Forge.Editing.State
{
    /// <summary>
    /// Where the user left off in one world: the space on show, where the camera was in each space, the tool, what the
    /// assets browser showed and what was being placed, the time of day, and the row gaps set per object. Editor state,
    /// kept beside the world's edit plugin, never in it: plugins are data that is shared, this is one user's view of it.
    /// Every value is optional (null: never set, the editor's default stands); ids that no longer exist are skipped.
    /// </summary>
    public sealed class WorldViewState
    {
        /// <summary>The space on show, by record id.</summary>
        public string Space { get; set; }

        /// <summary>The camera's last pose per space (record id), in world coordinates.</summary>
        public Dictionary<string, SavedPose> Cameras { get; set; }

        /// <summary>The picked tool, by name (Select, Place, …).</summary>
        public string Tool { get; set; }

        /// <summary>The assets browser's tab key.</summary>
        public string AssetsTab { get; set; }

        /// <summary>The assets browser's search text.</summary>
        public string AssetsSearch { get; set; }

        /// <summary>The base object the placing tools lay, by record id.</summary>
        public string Placing { get; set; }

        /// <summary>The time of day the editor shows.</summary>
        public SavedClock Clock { get; set; }

        /// <summary>The row gaps set per base object (record id).</summary>
        public Dictionary<string, SavedGap> Gaps { get; set; }
    }
}
