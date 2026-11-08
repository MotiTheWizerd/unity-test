using System.Collections.Generic;
using Forge.Data.Records;
using Forge.Game.Combat;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The combat styles the base game ships (<see cref="CoreCombatStyles"/>), each move the move set's clip that plays it:
    /// one-handed, drawn from the hip and a slash that costs nothing; archery, drawn from the back (no shot yet).
    /// </summary>
    static class CoreCombatStyleSet
    {
        /// <summary>The move set's move a one-handed attack plays.</summary>
        public const string SwordSlash = "SwordSlash";

        /// <summary>The move set's moves a one-handed weapon is drawn and put away by: the hip draw.</summary>
        public const string SwordDraw = "Draw";
        public const string SwordSheathe = "Sheathe";

        /// <summary>The move set's moves a bow is taken from the back and put back by.</summary>
        public const string BowDraw = "BowDraw";
        public const string BowSheathe = "BowSheathe";

        public static IEnumerable<CombatStyleRecord> All()
        {
            yield return new CombatStyleRecord(CoreCombatStyles.OneHanded, "One-Handed", new[]
            {
                new CombatMove(CombatMoveKind.Attack, SwordSlash),
                new CombatMove(CombatMoveKind.Draw, SwordDraw),
                new CombatMove(CombatMoveKind.Sheathe, SwordSheathe),
            });
            yield return new CombatStyleRecord(CoreCombatStyles.Archery, "Archery", new[]
            {
                new CombatMove(CombatMoveKind.Draw, BowDraw),
                new CombatMove(CombatMoveKind.Sheathe, BowSheathe),
            });
        }
    }
}
