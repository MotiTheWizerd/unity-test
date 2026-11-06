using Forge.Editing.State;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// A panel section whose layout is kept between sessions (the dock open, the browser's height, a tab). It writes
    /// its own part of the kept state and takes it back; the panel calls it for every section that is one.
    /// </summary>
    public interface IKeptSection
    {
        void Capture(EditorPrefs prefs, WorldViewState world);

        /// <summary>Takes back the kept layout; values never kept, or that no longer fit, leave the section as it is.</summary>
        void Restore(EditorPrefs prefs, WorldViewState world);
    }
}
