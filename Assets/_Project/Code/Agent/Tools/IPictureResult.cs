using System.Collections.Generic;
using Forge.Editing.Commands;

namespace Forge.Agent.Tools
{
    /// <summary>
    /// A command result that brings pictures back (the look command's view): the model sees them beside the result's
    /// JSON, which leaves <see cref="Pictures"/> out.
    /// </summary>
    public interface IPictureResult : ICommandResult
    {
        IReadOnlyList<AgentImage> Pictures { get; }
    }
}
