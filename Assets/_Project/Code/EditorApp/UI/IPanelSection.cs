namespace Forge.EditorApp.UI
{
    /// <summary>
    /// One section of the editor panel. It binds to its elements in the panel's
    /// UXML when constructed, sends user actions to the editor, and mirrors editor
    /// state on <see cref="Refresh"/> (called every frame). Keyboard shortcuts can
    /// change the same state, so the UI always reads it back rather than tracking it.
    /// </summary>
    public interface IPanelSection
    {
        void Refresh();
    }
}
