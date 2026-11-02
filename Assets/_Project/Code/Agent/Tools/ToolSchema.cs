using System;
using Forge.Editing.Commands;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.Tools
{
    /// <summary>
    /// The JSON schema of a command's arguments, read from the same <see cref="ParamAttribute"/>s the command
    /// checks before it runs, so what the model is told and what is enforced can't drift apart.
    /// </summary>
    public static class ToolSchema
    {
        public static JObject For(Type argumentsType)
        {
            var properties = new JObject();
            var required = new JArray();
            foreach (var parameter in CommandParameters.Of(argumentsType))
            {
                properties[parameter.Name] = Describe(parameter);
                if (!parameter.Info.Optional)
                    required.Add(parameter.Name);
            }

            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false,
            };
            if (required.Count > 0)
                schema["required"] = required;
            return schema;
        }

        static JObject Describe(CommandParameter parameter)
        {
            var info = parameter.Info;
            JObject schema;
            if (parameter.Kind == ParameterKind.List)
            {
                schema = new JObject
                {
                    ["type"] = "array",
                    ["items"] = ValueSchema(parameter.ItemKind.Value, parameter.ItemType),
                };
                if (info.MinItems > 0)
                    schema["minItems"] = info.MinItems;
                if (info.MaxItems > 0)
                    schema["maxItems"] = info.MaxItems;
            }
            else
            {
                schema = ValueSchema(parameter.Kind, parameter.ValueType);
            }

            schema["description"] = info.Description;
            if (!double.IsNaN(info.Minimum))
                schema["minimum"] = info.Minimum;
            if (!double.IsNaN(info.Maximum))
                schema["maximum"] = info.Maximum;
            return schema;
        }

        static JObject ValueSchema(ParameterKind kind, Type type)
        {
            switch (kind)
            {
                case ParameterKind.Boolean:
                    return new JObject { ["type"] = "boolean" };
                case ParameterKind.Integer:
                    return new JObject { ["type"] = "integer" };
                case ParameterKind.Number:
                    return new JObject { ["type"] = "number" };
                case ParameterKind.String:
                    return new JObject { ["type"] = "string" };
                case ParameterKind.Object:
                    return For(type);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Lists of lists are not supported.");
            }
        }
    }
}
