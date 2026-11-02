namespace Forge.Editing.Commands
{
    /// <summary>What a command reports back: at least one plain sentence saying what happened.</summary>
    public interface ICommandResult
    {
        /// <summary>What the command did, e.g. "Placed 14 Wooden Fence along 46.5 m.", for people and for the agent.</summary>
        string Summary { get; }
    }
}
