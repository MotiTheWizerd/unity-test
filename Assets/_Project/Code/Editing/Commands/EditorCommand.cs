using System;
using System.Linq;

namespace Forge.Editing.Commands
{
    /// <summary>
    /// Base of every command: checks the typed arguments against their <see cref="ParamAttribute"/>s, then
    /// runs. <typeparamref name="TResult"/> is plain data that tells the caller what happened.
    /// </summary>
    public abstract class EditorCommand<TArguments, TResult> : IEditorCommand where TArguments : class
    {
        /// <summary>Longest command name; tool protocols cap names at 64 characters.</summary>
        public const int MaxNameLength = 64;

        protected EditorCommand(string name, string description)
        {
            if (!IsValidName(name))
                throw new ArgumentException($"'{name}' is not a valid command name: use lowercase snake_case, at most {MaxNameLength} characters.", nameof(name));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("A command needs a description.", nameof(description));

            Name = name;
            Description = description;
        }

        public string Name { get; }

        public string Description { get; }

        public Type ArgumentsType => typeof(TArguments);

        public TResult Execute(TArguments arguments)
        {
            CommandArguments.Validate(arguments);
            return Run(arguments);
        }

        object IEditorCommand.Execute(object arguments)
        {
            if (arguments != null && !(arguments is TArguments))
                throw new ArgumentException($"{Name} takes {typeof(TArguments).Name}, not {arguments.GetType().Name}.", nameof(arguments));
            return Execute((TArguments)arguments);
        }

        /// <summary>Does the work; the arguments have been checked.</summary>
        protected abstract TResult Run(TArguments arguments);

        static bool IsValidName(string name) =>
            !string.IsNullOrEmpty(name) && name.Length <= MaxNameLength
            && name[0] >= 'a' && name[0] <= 'z'
            && name.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_');
    }
}
