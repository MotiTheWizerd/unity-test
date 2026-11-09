using System;

namespace Forge.UnityEditor.Content
{
    /// <summary>What a move is to the animator, from where it sits in its move set (<see cref="HumanoidMoves.RoleOf"/>).</summary>
    enum MoveRole
    {
        /// <summary>Walked on the ground, blended by speed (Locomotion/: Idle, Run, DrawnRun).</summary>
        Ground,

        /// <summary>Played in the air (Air/: Jump, Fall, Land).</summary>
        Air,

        /// <summary>A combat style's drawn idle, its drawn ground moves standing in it (Styles/Archery/BowIdle).</summary>
        Stance,

        /// <summary>An action on the arms alone, whatever the legs do (a draw, a sheathe).</summary>
        ArmsAction,

        /// <summary>An action on the whole upper body, the legs still walking (a slash, a shot).</summary>
        UpperBodyAction,
    }

    /// <summary>
    /// The moves a humanoid's animations are made of, each one file named after it in a move set
    /// (Content/Animations/{Set}/...), and what each is, by the folder it sits in:
    /// <list type="bullet">
    /// <item><see cref="LocomotionFolder"/>: the ground moves, blended by speed, and their versions with weapons drawn.</item>
    /// <item><see cref="AirFolder"/>: the jump, the fall, the landing.</item>
    /// <item><see cref="StylesFolder"/>/{Style}: a combat style's moves; its {Style}Idle is its stance, the rest actions.</item>
    /// <item>Any other folder (Interact, Emotes, ...): actions.</item>
    /// <item><see cref="BuiltFolder"/>: what the builder makes (the controller, its masks), never moves.</item>
    /// </list>
    /// An action plays on the arms alone when it draws or sheathes, else on the upper body. A move's name is its own
    /// across the set's folders, since content names it so ("BowShot"). The import (<see cref="AnimationImport"/>) loops
    /// what repeats; the animator builder (<see cref="HumanoidAnimatorBuilder"/>) plays whichever of them are present.
    /// </summary>
    static class HumanoidMoves
    {
        public const string LocomotionFolder = "Locomotion";
        public const string AirFolder = "Air";
        public const string StylesFolder = "Styles";
        public const string BuiltFolder = "_Built";

        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string Run = "Run";
        public const string Sprint = "Sprint";
        public const string Jump = "Jump";
        public const string Fall = "Fall";
        public const string Land = "Land";

        /// <summary>Drawing the weapons (the arms only, whatever the legs do).</summary>
        public const string Draw = "Draw";

        /// <summary>Putting the weapons away (the arms only).</summary>
        public const string Sheathe = "Sheathe";

        /// <summary>The ground moves, slowest first: the speed blend's order.</summary>
        public static readonly string[] Ground = { Idle, Walk, Run, Sprint };

        /// <summary>The ground moves with weapons drawn, in the same order; each falls back to its plain move when missing.</summary>
        public static readonly string[] DrawnGround = { Drawn(Idle), Drawn(Walk), Drawn(Run), Drawn(Sprint) };

        /// <summary>The air moves.</summary>
        public static readonly string[] Air = { Jump, Fall, Land };

        /// <summary>A ground move's drawn version ("DrawnRun").</summary>
        public static string Drawn(string move) => "Drawn" + move;

        /// <summary>
        /// What the move at <paramref name="path"/> (in its set, "Styles/Archery/BowShot.fbx", '/' or '\' between
        /// folders) is, by its folder and name; null for none (outside any folder, or among what is built). Pure, so
        /// testable.
        /// </summary>
        public static MoveRole? RoleOf(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            var parts = path.Replace('\\', '/').Split('/');
            if (parts.Length < 2)
                return null;
            var move = MoveName(parts[parts.Length - 1]);
            switch (parts[0])
            {
                case BuiltFolder: return null;
                case LocomotionFolder: return MoveRole.Ground;
                case AirFolder: return MoveRole.Air;
                case StylesFolder when parts.Length > 2 && IsStanceName(move): return MoveRole.Stance;
                default: return ArmsOnly(move) ? MoveRole.ArmsAction : MoveRole.UpperBodyAction;
            }
        }

        /// <summary>Whether <paramref name="move"/> plays on the arms alone (drawing and sheathing, a style's too: "BowDraw").</summary>
        public static bool ArmsOnly(string move) =>
            move != null && (move.EndsWith(Draw, StringComparison.Ordinal) || move.EndsWith(Sheathe, StringComparison.Ordinal));

        /// <summary>Whether a move of <paramref name="role"/> named <paramref name="move"/> repeats while it lasts (standing, walking, falling) rather than playing once.</summary>
        public static bool Loops(MoveRole role, string move) =>
            role == MoveRole.Ground || role == MoveRole.Stance || (role == MoveRole.Air && move == Fall);

        /// <summary>
        /// Whether a move of <paramref name="role"/> named <paramref name="move"/> lifts the body off the ground (a jump).
        /// The body's own rise already does that, so the clip's rise is left out of its pose; other moves keep theirs.
        /// </summary>
        public static bool Lifts(MoveRole role, string move) => role == MoveRole.Air && move == Jump;

        /// <summary>
        /// Where a move named <paramref name="move"/> of <paramref name="role"/> ought to be, when the builder only looks
        /// for it elsewhere: a known ground or air move outside its folder; else null.
        /// </summary>
        public static string Misplaced(string move, MoveRole role)
        {
            if (role != MoveRole.Ground && (Array.IndexOf(Ground, move) >= 0 || Array.IndexOf(DrawnGround, move) >= 0))
                return LocomotionFolder;
            if (role != MoveRole.Air && Array.IndexOf(Air, move) >= 0)
                return AirFolder;
            return null;
        }

        /// <summary>The move a file is ("Styles/Archery/BowShot.fbx": "BowShot").</summary>
        public static string MoveName(string file)
        {
            var name = file.Replace('\\', '/');
            name = name.Substring(name.LastIndexOf('/') + 1);
            var dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : name;
        }

        // A style's stance is its idle ("BowIdle").
        static bool IsStanceName(string move) => move.EndsWith(Idle, StringComparison.Ordinal) && move != Idle;
    }
}
