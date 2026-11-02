using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Editing.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.Tools
{
    /// <summary>
    /// Runs commands for a model: reads its JSON arguments into the command's arguments class, runs the command
    /// and writes the result back as JSON. Bad arguments and refusals come back as error results the model can
    /// read and correct; they never throw. Call it on the thread that owns the edit session.
    /// </summary>
    public sealed class ToolInvoker
    {
        readonly CommandRegistry _registry;
        readonly JsonSerializer _serializer;

        public ToolInvoker(CommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new ParameterNameResolver(),
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MaxDepth = 32,
            });
            Tools = registry.Commands
                .Select(c => new ToolDefinition(c.Name, c.Description, ToolSchema.For(c.ArgumentsType)))
                .ToList();
        }

        public IReadOnlyList<ToolDefinition> Tools { get; }

        public ToolResult Invoke(string name, JObject arguments)
        {
            if (!_registry.TryGet(name, out var command))
                return ToolResult.Failure($"There is no tool '{name}'.");

            object typed;
            try
            {
                typed = (arguments ?? new JObject()).ToObject(command.ArgumentsType, _serializer);
            }
            catch (JsonException ex)
            {
                return ToolResult.Failure($"The arguments don't fit {name}: {ex.Message}");
            }

            ICommandResult result;
            try
            {
                result = command.Execute(typed);
            }
            // The same refusals the editor's own edits report instead of throwing (see EditorContext.TryEdit).
            catch (Exception ex) when (ex is CommandException || ex is DataValidationException || ex is ArgumentException || ex is InvalidOperationException)
            {
                return ToolResult.Failure(ex.Message);
            }

            var content = JToken.FromObject(result, _serializer).ToString(Formatting.None);
            return ToolResult.Success(content, result.Summary);
        }
    }
}
