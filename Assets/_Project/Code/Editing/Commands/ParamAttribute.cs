using System;

namespace Forge.Editing.Commands
{
    /// <summary>
    /// Describes one argument of an <see cref="IEditorCommand"/>: what it means, whether it may be left out, and
    /// the range it must be in. The same attribute documents the argument for the agent (its tool schema) and is
    /// checked before every run, so a command's rules are written once. Every public property of an arguments
    /// class needs one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ParamAttribute : Attribute
    {
        public ParamAttribute(string description)
        {
            Description = description;
        }

        public string Description { get; }

        /// <summary>May be left out (null); the property's type must then be nullable.</summary>
        public bool Optional { get; set; }

        /// <summary>Smallest allowed value of a number; NaN for no limit.</summary>
        public double Minimum { get; set; } = double.NaN;

        /// <summary>Largest allowed value of a number; NaN for no limit.</summary>
        public double Maximum { get; set; } = double.NaN;

        /// <summary>Fewest items a list needs; 0 for no limit.</summary>
        public int MinItems { get; set; }

        /// <summary>Most items a list may have; 0 for no limit.</summary>
        public int MaxItems { get; set; }
    }
}
