using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Records
{
    /// <summary>
    /// A way of fighting (one-handed blade, two-handed, shield, staff, a school of magic): the moves it has
    /// (<see cref="Moves"/>, one of each kind). Weapons name theirs (<see cref="WeaponPart.Style"/>), so a new style, a
    /// spear's say, is a new record and its clips, not new code.
    /// </summary>
    public sealed class CombatStyleRecord : Record
    {
        /// <summary>Name prefix of combat style records, e.g. "forge.core:combat_style/one_handed".</summary>
        public const string NamePrefix = "combat_style/";

        /// <param name="moves">Its moves, one of each kind; at least one.</param>
        public CombatStyleRecord(RecordId id, string displayName, IEnumerable<CombatMove> moves)
            : base(id)
        {
            if (!id.Name.StartsWith(NamePrefix, StringComparison.Ordinal))
                throw new ArgumentException($"Combat style ids are named '{NamePrefix}…', not '{id.Name}'.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            var list = moves?.ToArray() ?? throw new ArgumentNullException(nameof(moves));
            if (list.Length == 0)
                throw new ArgumentException("A combat style has at least one move.", nameof(moves));
            if (list.Any(move => move == null))
                throw new ArgumentException("A move can't be missing.", nameof(moves));
            if (list.Select(move => move.Kind).Distinct().Count() != list.Length)
                throw new ArgumentException("A combat style has one move of each kind.", nameof(moves));

            DisplayName = displayName;
            Moves = list;
        }

        /// <summary>What a player reads ("One-Handed").</summary>
        public string DisplayName { get; }

        /// <summary>Its moves, one of each kind; never empty.</summary>
        public IReadOnlyList<CombatMove> Moves { get; }

        /// <summary>Its move of <paramref name="kind"/>; false when it has none (a shield doesn't attack).</summary>
        public bool TryGetMove(CombatMoveKind kind, out CombatMove move)
        {
            foreach (var candidate in Moves)
            {
                if (candidate.Kind == kind)
                {
                    move = candidate;
                    return true;
                }
            }

            move = null;
            return false;
        }

        public static RecordId IdFor(string ns, string name) => new RecordId(ns, NamePrefix + name);
    }
}
