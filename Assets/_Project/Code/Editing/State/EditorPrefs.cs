namespace Forge.Editing.State
{
    /// <summary>
    /// The user's editor preferences, the same in every world: how the panel is laid out and how the tools are set.
    /// Kept between sessions in one file per user. Every value is optional: null means never set, so the editor's
    /// default stands, and a value added later reads as null from an older file.
    /// </summary>
    public sealed class EditorPrefs
    {
        /// <summary>Whether the side dock on the right is open.</summary>
        public bool? SideDockOpen { get; set; }

        /// <summary>The side dock's page (its key: agent, materials).</summary>
        public string SideDockPage { get; set; }

        /// <summary>Whether the assets browser is collapsed to its header.</summary>
        public bool? AssetsCollapsed { get; set; }

        /// <summary>The assets browser's dragged height in panel pixels; null while it was never dragged.</summary>
        public float? AssetsHeight { get; set; }

        /// <summary>The settings card's page (its tab key).</summary>
        public string SettingsTab { get; set; }

        /// <summary>Whether the settings card is open.</summary>
        public bool? SettingsOpen { get; set; }

        /// <summary>The fly speed multiplier.</summary>
        public float? FlySpeed { get; set; }

        /// <summary>Whether the fly speed follows the camera's height.</summary>
        public bool? FlySpeedByHeight { get; set; }

        /// <summary>The brush's radius in meters.</summary>
        public float? BrushRadius { get; set; }

        /// <summary>The brush's strength.</summary>
        public float? BrushStrength { get; set; }

        /// <summary>The sculpt brush's mode, by name (Raise, Lower, …).</summary>
        public string BrushMode { get; set; }

        /// <summary>Whether sculpting carries placed objects with the ground.</summary>
        public bool? ObjectsFollowTerrain { get; set; }

        /// <summary>Whether new objects get their random turn and size.</summary>
        public bool? RandomizePlacement { get; set; }

        /// <summary>Whether objects that ask for it are tilted to the slope.</summary>
        public bool? AlignToSlope { get; set; }

        /// <summary>Whether the outline and fill tools size their rectangle to whole pieces.</summary>
        public bool? WholePieces { get; set; }
    }
}
