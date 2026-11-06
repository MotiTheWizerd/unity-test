using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// Part of an attribute's base that comes from another: <see cref="Per"/> points for each point of
    /// <see cref="Attribute"/> (its full value; a resource's most, not what is left). Carry weight gaining half a pound per
    /// point of stamina is one term.
    /// </summary>
    public sealed class AttributeTerm
    {
        public AttributeTerm(RecordId attribute, float per)
        {
            if (attribute.IsDefault)
                throw new ArgumentException("A term names the attribute it reads.", nameof(attribute));
            if (float.IsNaN(per) || float.IsInfinity(per))
                throw new ArgumentOutOfRangeException(nameof(per), per, "Must be a finite number.");
            Attribute = attribute;
            Per = per;
        }

        /// <summary>The attribute read.</summary>
        public RecordId Attribute { get; }

        /// <summary>Points added for each of its points (negative to take away).</summary>
        public float Per { get; }
    }
}
