namespace Forge.EditorApp.Tools
{
    /// <summary>An interaction mode of the editor. Exactly one tool is active at a time.</summary>
    public interface IEditorTool
    {
        void Activate();

        /// <summary>Called when another tool takes over; must cancel any action in progress.</summary>
        void Deactivate();

        void Tick(in ToolInput input);
    }
}
