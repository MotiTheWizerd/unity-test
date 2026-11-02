using System;

namespace Forge.Editing.Commands
{
    /// <summary>
    /// An editor action with a stable name, a description and typed arguments, defined once and callable by
    /// buttons, shortcuts and the world-building agent alike (like Blender's operators). The arguments are a
    /// plain class whose properties carry <see cref="ParamAttribute"/>s: they describe each argument to every
    /// caller and are checked before the command runs.
    /// </summary>
    public interface IEditorCommand
    {
        /// <summary>Stable snake_case name, e.g. "place_line".</summary>
        string Name { get; }

        /// <summary>What the command does and when to use it, written for people and for the agent.</summary>
        string Description { get; }

        /// <summary>The class of the arguments <see cref="Execute"/> takes.</summary>
        Type ArgumentsType { get; }

        /// <summary>Checks <paramref name="arguments"/> against their parameters, then runs. Throws <see cref="CommandException"/> when refused.</summary>
        ICommandResult Execute(object arguments);
    }
}
