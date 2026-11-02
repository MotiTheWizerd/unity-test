namespace Forge.Agent
{
    /// <summary>Who or what a transcript line comes from.</summary>
    public enum ChatRole
    {
        User,
        Agent,

        /// <summary>A tool call the agent made, e.g. "Placed 14 Wooden Fence along 46.5 m."</summary>
        Activity,

        /// <summary>The editor's own word: accepted, discarded, stopped.</summary>
        Notice,

        Error,
    }
}
