using System;

namespace Forge.Game.Interaction
{
    /// <summary>
    /// What using a target would do, as the prompt shows it, Skyrim's way: the target's <see cref="Name"/>, the
    /// <see cref="Verb"/> the use key does ("Take") and, optionally, a line of <see cref="Detail"/> ("Weight 9   Value 75").
    /// </summary>
    public sealed class InteractionOffer
    {
        public InteractionOffer(string verb, string name, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(verb))
                throw new ArgumentException("An offer says what it does.", nameof(verb));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An offer names what it is for.", nameof(name));

            Verb = verb;
            Name = name;
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail;
        }

        public string Verb { get; }

        public string Name { get; }

        /// <summary>A line about the target, or null.</summary>
        public string Detail { get; }
    }
}
