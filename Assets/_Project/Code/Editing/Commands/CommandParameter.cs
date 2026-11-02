using System;
using System.Reflection;

namespace Forge.Editing.Commands
{
    /// <summary>One argument of a command, as its <see cref="ParamAttribute"/> and property describe it.</summary>
    public sealed class CommandParameter
    {
        internal CommandParameter(PropertyInfo property, ParamAttribute info, ParameterKind kind, Type valueType, ParameterKind? itemKind, Type itemType)
        {
            Property = property;
            Info = info;
            Kind = kind;
            ValueType = valueType;
            ItemKind = itemKind;
            ItemType = itemType;
            Name = CommandParameters.NameOf(property.Name);
        }

        /// <summary>The argument's snake_case name, as callers write it.</summary>
        public string Name { get; }

        public PropertyInfo Property { get; }

        public ParamAttribute Info { get; }

        public ParameterKind Kind { get; }

        /// <summary>The property's type without <see cref="Nullable{T}"/>: the scalar, the nested class or the list type.</summary>
        public Type ValueType { get; }

        /// <summary>For lists, what each item holds; null otherwise.</summary>
        public ParameterKind? ItemKind { get; }

        /// <summary>For lists, the item type; null otherwise.</summary>
        public Type ItemType { get; }

        public object ValueOf(object arguments) => Property.GetValue(arguments);
    }
}
