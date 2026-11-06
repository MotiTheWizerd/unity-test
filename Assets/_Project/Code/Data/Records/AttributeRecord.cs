using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Records
{
    /// <summary>
    /// One thing an actor has a number for, like a Bethesda actor value: a <see cref="AttributeKind.Resource"/> that is
    /// spent and refills (health, stamina, magicka) or a <see cref="AttributeKind.Stat"/> that only is (carry weight, a
    /// skill, how much a sprint costs). Data, not code: a mod adds an attribute ("thirst") by adding a record, or changes
    /// one everywhere by overriding it. Every actor starts from <see cref="BaseValue"/> plus its <see cref="Terms"/>;
    /// what items, effects and perks do to it is the game's (modifiers), not the record's.
    /// </summary>
    public sealed class AttributeRecord : Record
    {
        /// <summary>Name prefix of attribute records, e.g. "forge.core:attribute/health".</summary>
        public const string NamePrefix = "attribute/";

        /// <param name="min">The least it can be once modified.</param>
        /// <param name="max">The most it can be once modified; null for no limit.</param>
        /// <param name="terms">What other attributes add to its base ("+0.5 per stamina"); null or empty for none.</param>
        /// <param name="regenRate">For a resource: the attribute holding how many points it refills a second; null for none.</param>
        /// <param name="regenDelay">For a resource: the attribute holding how many seconds after being spent it waits to refill; null for none.</param>
        public AttributeRecord(
            RecordId id,
            string displayName,
            AttributeKind kind,
            float baseValue,
            float min = 0f,
            float? max = null,
            IReadOnlyList<AttributeTerm> terms = null,
            RecordId? regenRate = null,
            RecordId? regenDelay = null)
            : base(id)
        {
            if (!id.Name.StartsWith(NamePrefix, StringComparison.Ordinal))
                throw new ArgumentException($"Attribute ids are named '{NamePrefix}…', not '{id.Name}'.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (!Enum.IsDefined(typeof(AttributeKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown attribute kind.");
            if (!IsFinite(baseValue))
                throw new ArgumentOutOfRangeException(nameof(baseValue), baseValue, "Must be a finite number.");
            if (!IsFinite(min))
                throw new ArgumentOutOfRangeException(nameof(min), min, "Must be a finite number.");
            if (max.HasValue && !(IsFinite(max.Value) && max.Value >= min))
                throw new ArgumentOutOfRangeException(nameof(max), max, $"Must be a finite number no less than the min ({min}).");
            if (terms != null && terms.Any(term => term == null))
                throw new ArgumentException("An attribute's terms can't be missing.", nameof(terms));
            if (terms != null && terms.Any(term => term.Attribute == id))
                throw new ArgumentException("An attribute can't add to itself.", nameof(terms));
            if (kind != AttributeKind.Resource && (regenRate.HasValue || regenDelay.HasValue))
                throw new ArgumentException("Only a resource refills.", nameof(regenRate));
            if (regenRate.HasValue && (regenRate.Value.IsDefault || regenRate.Value == id))
                throw new ArgumentException("A regen rate names another attribute; leave it out for none.", nameof(regenRate));
            if (regenDelay.HasValue && (regenDelay.Value.IsDefault || regenDelay.Value == id))
                throw new ArgumentException("A regen delay names another attribute; leave it out for none.", nameof(regenDelay));

            DisplayName = displayName;
            Kind = kind;
            BaseValue = baseValue;
            Min = min;
            Max = max;
            Terms = terms != null && terms.Count > 0 ? terms.ToArray() : null;
            RegenRate = regenRate;
            RegenDelay = regenDelay;
        }

        public string DisplayName { get; }

        public AttributeKind Kind { get; }

        /// <summary>What every actor starts with, before its terms and modifiers.</summary>
        public float BaseValue { get; }

        public float Min { get; }

        /// <summary>The most it can be; null for no limit (and left out of the file).</summary>
        public float? Max { get; }

        /// <summary>What other attributes add to its base; null when none (and left out of the file).</summary>
        public IReadOnlyList<AttributeTerm> Terms { get; }

        /// <summary>The attribute holding how many points a second this resource refills; null for none.</summary>
        public RecordId? RegenRate { get; }

        /// <summary>The attribute holding the seconds this resource waits after being spent before it refills; null for none.</summary>
        public RecordId? RegenDelay { get; }

        /// <summary>Every attribute this one reads: its terms' and its regen's.</summary>
        public IEnumerable<RecordId> Reads()
        {
            if (Terms != null)
            {
                foreach (var term in Terms)
                    yield return term.Attribute;
            }

            if (RegenRate.HasValue)
                yield return RegenRate.Value;
            if (RegenDelay.HasValue)
                yield return RegenDelay.Value;
        }

        public static RecordId IdFor(string ns, string name) => new RecordId(ns, NamePrefix + name);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
