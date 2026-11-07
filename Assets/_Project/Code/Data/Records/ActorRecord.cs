using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Records
{
    /// <summary>
    /// An actor as the game begins it, like a Bethesda NPC record (the player's included): its name and what it starts
    /// out carrying and holding (<see cref="Items"/>). Data, so a mod changes the player's starting kit by overriding the
    /// player's record. More of an actor (its look, its attributes) joins it as the world's characters come.
    /// </summary>
    public sealed class ActorRecord : Record
    {
        /// <summary>Name prefix of actor records, e.g. "forge.core:actor/player".</summary>
        public const string NamePrefix = "actor/";

        /// <param name="items">What it starts out carrying, each item once; null for nothing.</param>
        public ActorRecord(RecordId id, string displayName, IEnumerable<ActorItem> items = null)
            : base(id)
        {
            if (!id.Name.StartsWith(NamePrefix, StringComparison.Ordinal))
                throw new ArgumentException($"Actor ids are named '{NamePrefix}…', not '{id.Name}'.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            var list = items?.ToArray() ?? Array.Empty<ActorItem>();
            if (list.Any(item => item == null))
                throw new ArgumentException("An item can't be missing.", nameof(items));
            if (list.Select(item => item.Item).Distinct().Count() != list.Length)
                throw new ArgumentException("Each item is listed once; say how many in its count.", nameof(items));

            DisplayName = displayName;
            Items = list;
        }

        /// <summary>What a player reads ("Player").</summary>
        public string DisplayName { get; }

        /// <summary>What it starts out carrying, and what of it is equipped; empty for nothing.</summary>
        public IReadOnlyList<ActorItem> Items { get; }

        public static RecordId IdFor(string ns, string name) => new RecordId(ns, NamePrefix + name);
    }
}
