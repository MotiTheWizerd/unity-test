using System.Collections.Generic;
using Forge.Data.Records;
using Forge.Game.Combat;

namespace Forge.UnityEditor.Content
{
    /// <summary>The combat styles the base game ships (<see cref="CoreCombatStyles"/>): one-handed, a slash that costs nothing.</summary>
    static class CoreCombatStyleSet
    {
        /// <summary>The move set's move a one-handed attack plays.</summary>
        public const string SwordSlash = "SwordSlash";

        public static IEnumerable<CombatStyleRecord> All()
        {
            yield return new CombatStyleRecord(CoreCombatStyles.OneHanded, "One-Handed", new[]
            {
                new CombatMove(CombatMoveKind.Attack, SwordSlash),
            });
        }
    }
}
