using System.Collections.Generic;
using Forge.Data.Records;
using Forge.Game.Combat;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The combat styles the base game ships (<see cref="CoreCombatStyles"/>), each move the move set's clip that plays it:
    /// one-handed, drawn from the hip and a slash that costs nothing; archery, drawn from the back, its attack an arrow
    /// drawn while held and loosed on release, the string let go and the bow brought down after, stood in with the bow
    /// held low; a shield, raised against blows while held and jolted when one lands on it.
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

        /// <summary>The move set's move an archery attack plays: an arrow from the quiver, nocked and drawn to full.</summary>
        public const string BowShot = "BowShot";

        /// <summary>The move set's move that plays once the arrow is loosed: the string let go, the bow brought down.</summary>
        public const string BowRelease = "BowRelease";

        /// <summary>The move set's stance a drawn bow is stood in: held low in the bow hand, the other at the side.</summary>
        public const string BowIdle = "BowIdle";

        /// <summary>The move set's moves a shield is raised by, held up while blocking, and jolted by when it takes a blow.</summary>
        public const string ShieldBlock = "ShieldBlock";
        public const string ShieldImpact = "ShieldImpact";

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
                new CombatMove(CombatMoveKind.Attack, BowShot, held: true, release: BowRelease),
                new CombatMove(CombatMoveKind.Draw, BowDraw),
                new CombatMove(CombatMoveKind.Sheathe, BowSheathe),
                new CombatMove(CombatMoveKind.Stance, BowIdle),
            });
            yield return new CombatStyleRecord(CoreCombatStyles.Shield, "Shield", new[]
            {
                new CombatMove(CombatMoveKind.Block, ShieldBlock, held: true),
                new CombatMove(CombatMoveKind.Blocked, ShieldImpact),
            });
        }
    }
}
