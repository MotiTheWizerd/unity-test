using System;
using Forge.Data.Records;

namespace Forge.Game.Attributes
{
    /// <summary>
    /// Applies an <see cref="Effect"/> to an actor's attributes: damage drains a resource, restore refills it. The one
    /// place any blow, spell or potion changes an actor. What the actor's attributes don't have, or isn't a resource, is
    /// left alone (a mod's effect on an attribute this world lacks does nothing). Engine-free.
    /// </summary>
    public static class AttributeEffects
    {
        /// <summary>Applies <paramref name="effect"/> to <paramref name="target"/>; how much it changed its attribute.</summary>
        public static float Apply(Effect effect, ActorAttributes target)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!target.Set.Contains(effect.Attribute) ||
                target.Set.Records[target.Set.SlotOf(effect.Attribute)].Kind != AttributeKind.Resource)
                return 0f;

            switch (effect.Kind)
            {
                case EffectKind.Damage:
                    return target.Drain(effect.Attribute, effect.Amount);
                case EffectKind.Restore:
                    var before = target.Current(effect.Attribute);
                    target.Restore(effect.Attribute, effect.Amount);
                    return target.Current(effect.Attribute) - before;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind, "Unknown effect kind.");
            }
        }
    }
}
