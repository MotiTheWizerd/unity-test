using Forge.Data;
using Forge.Data.Records;

namespace Forge.Game.Attributes
{
    /// <summary>
    /// The attributes the game's own rules read by name, as Skyrim's engine knows Health and Stamina. They are records of
    /// the base game (forge.core), so a mod retunes them by overriding the record; every other attribute a mod adds is
    /// pure data, read by whatever reads it.
    /// </summary>
    public static class CoreAttributes
    {
        /// <summary>The base game's plugin, which defines them.</summary>
        public const string Namespace = "forge.core";

        /// <summary>Resource: hurt by falls (and later blows); nothing more happens at zero yet.</summary>
        public static readonly RecordId Health = Id("health");

        public static readonly RecordId HealthRegen = Id("health_regen");

        public static readonly RecordId HealthRegenDelay = Id("health_regen_delay");

        /// <summary>Resource: spent by sprinting and jumping.</summary>
        public static readonly RecordId Stamina = Id("stamina");

        public static readonly RecordId StaminaRegen = Id("stamina_regen");

        public static readonly RecordId StaminaRegenDelay = Id("stamina_regen_delay");

        /// <summary>Resource: spent by spells, later.</summary>
        public static readonly RecordId Magicka = Id("magicka");

        public static readonly RecordId MagickaRegen = Id("magicka_regen");

        public static readonly RecordId MagickaRegenDelay = Id("magicka_regen_delay");

        /// <summary>Stat: how much the inventory holds before slowing the actor, later.</summary>
        public static readonly RecordId CarryWeight = Id("carry_weight");

        /// <summary>Stat: stamina a second of sprinting costs.</summary>
        public static readonly RecordId SprintCost = Id("sprint_cost");

        /// <summary>Stat: stamina a jump costs.</summary>
        public static readonly RecordId JumpCost = Id("jump_cost");

        /// <summary>Stat: meters an actor falls unhurt.</summary>
        public static readonly RecordId FallSafeHeight = Id("fall_safe_height");

        /// <summary>Stat: health lost per meter fallen past the safe height.</summary>
        public static readonly RecordId FallDamage = Id("fall_damage");

        static RecordId Id(string name) => AttributeRecord.IdFor(Namespace, name);
    }
}
