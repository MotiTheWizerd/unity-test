using Forge.Data;
using Forge.Data.Records;

namespace Forge.Game.Combat
{
    /// <summary>
    /// The combat styles of the base game (forge.core), as its content names them. Records, so a mod retunes a style or
    /// adds one; the rules read none of them by name, only content does.
    /// </summary>
    public static class CoreCombatStyles
    {
        /// <summary>The base game's plugin, which defines them.</summary>
        public const string Namespace = "forge.core";

        /// <summary>A one-handed blade: a sword, an axe, a mace.</summary>
        public static readonly RecordId OneHanded = CombatStyleRecord.IdFor(Namespace, "one_handed");

        /// <summary>A bow: drawn from the back, held in the left hand.</summary>
        public static readonly RecordId Archery = CombatStyleRecord.IdFor(Namespace, "archery");
    }
}
