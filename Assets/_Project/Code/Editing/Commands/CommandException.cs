using System;

namespace Forge.Editing.Commands
{
    /// <summary>
    /// A command refused to run. The message says why, in words meant for whoever asked: a person in the
    /// editor or the agent, which reads it and tries again.
    /// </summary>
    public sealed class CommandException : Exception
    {
        public CommandException(string message) : base(message)
        {
        }
    }
}
