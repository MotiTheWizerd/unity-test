using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Forge.Editing.Commands
{
    /// <summary>
    /// Reads the parameters of an arguments class: one per public property, in declaration order, each with its
    /// <see cref="ParamAttribute"/>. Malformed classes (a property without the attribute, an optional value
    /// type, a range on a string) are programming errors and throw at once.
    /// </summary>
    public static class CommandParameters
    {
        public static IReadOnlyList<CommandParameter> Of(Type argumentsType)
        {
            if (argumentsType == null)
                throw new ArgumentNullException(nameof(argumentsType));

            return argumentsType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(p => p.MetadataToken)
                .Select(p => Read(argumentsType, p))
                .ToList();
        }

        /// <summary>The snake_case name callers use for a property, e.g. "RandomGap" → "random_gap".</summary>
        public static string NameOf(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException("A property name is required.", nameof(propertyName));

            var name = new StringBuilder(propertyName.Length + 4);
            for (var i = 0; i < propertyName.Length; i++)
            {
                var c = propertyName[i];
                if (char.IsUpper(c) && i > 0)
                {
                    var previous = propertyName[i - 1];
                    var nextIsLower = i + 1 < propertyName.Length && char.IsLower(propertyName[i + 1]);
                    if (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower))
                        name.Append('_');
                }

                name.Append(char.ToLowerInvariant(c));
            }

            return name.ToString();
        }

        static CommandParameter Read(Type owner, PropertyInfo property)
        {
            var where = $"{owner.Name}.{property.Name}";
            var info = property.GetCustomAttribute<ParamAttribute>()
                       ?? throw new InvalidOperationException($"{where} has no [Param]; every argument must be described.");
            if (string.IsNullOrWhiteSpace(info.Description))
                throw new InvalidOperationException($"{where} needs a description.");
            if (!property.CanRead || !property.CanWrite)
                throw new InvalidOperationException($"{where} must have a public getter and setter.");

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var kind = KindOf(type, where);
            if (info.Optional && property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null)
                throw new InvalidOperationException($"{where} is optional, so its type must be nullable.");

            var isNumber = kind == ParameterKind.Number || kind == ParameterKind.Integer;
            if (!isNumber && (!double.IsNaN(info.Minimum) || !double.IsNaN(info.Maximum)))
                throw new InvalidOperationException($"{where} has a range but is not a number.");
            if (kind != ParameterKind.List && (info.MinItems != 0 || info.MaxItems != 0))
                throw new InvalidOperationException($"{where} has an item count but is not a list.");

            ParameterKind? itemKind = null;
            Type itemType = null;
            if (kind == ParameterKind.List)
            {
                itemType = type.GetGenericArguments()[0];
                itemKind = KindOf(itemType, where + " items");
                if (itemKind == ParameterKind.List)
                    throw new InvalidOperationException($"{where} is a list of lists, which arguments don't support.");
            }

            return new CommandParameter(property, info, kind, type, itemKind, itemType);
        }

        static ParameterKind KindOf(Type type, string where)
        {
            if (type == typeof(bool))
                return ParameterKind.Boolean;
            if (type == typeof(int) || type == typeof(long))
                return ParameterKind.Integer;
            if (type == typeof(float) || type == typeof(double))
                return ParameterKind.Number;
            if (type == typeof(string))
                return ParameterKind.String;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return ParameterKind.List;
            if (type.IsClass && !type.IsAbstract)
                return ParameterKind.Object;
            throw new InvalidOperationException($"{where} is a {type.Name}, which arguments don't support.");
        }
    }
}
