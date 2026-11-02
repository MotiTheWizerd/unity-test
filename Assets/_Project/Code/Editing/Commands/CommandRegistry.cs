using System;
using System.Collections.Generic;

namespace Forge.Editing.Commands
{
    /// <summary>The commands an editor offers, by name, in the order they were added.</summary>
    public sealed class CommandRegistry
    {
        readonly List<IEditorCommand> _commands = new List<IEditorCommand>();
        readonly Dictionary<string, IEditorCommand> _byName = new Dictionary<string, IEditorCommand>(StringComparer.Ordinal);

        public IReadOnlyList<IEditorCommand> Commands => _commands;

        public void Add(IEditorCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (_byName.ContainsKey(command.Name))
                throw new ArgumentException($"A command named '{command.Name}' is already registered.", nameof(command));

            _commands.Add(command);
            _byName.Add(command.Name, command);
        }

        public bool TryGet(string name, out IEditorCommand command)
        {
            command = null;
            return name != null && _byName.TryGetValue(name, out command);
        }
    }
}
