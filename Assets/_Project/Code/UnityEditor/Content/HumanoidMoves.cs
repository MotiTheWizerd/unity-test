using System;

namespace Forge.UnityEditor.Content
{
    /// <summary>What a move is to the animator, from where it sits in its move set and what its settings say (<see cref="HumanoidMoves.RoleOf"/>).</summary>
    enum MoveRole
    {
        /// <summary>Walked on the ground, blended by speed, or by the way the body goes when the set sidesteps (Locomotion/: Idle, Run, DrawnRun, DrawnStrafe).</summary>
        Ground,

        /// <summary>Played in the air (Air/: Jump, Fall, Land, DrawnJump).</summary>
        Air,

        /// <summary>A combat style's drawn idle, its drawn ground moves standing in it (Styles/Archery/BowIdle).</summary>
        Stance,

        /// <summary>An action on the arms alone, whatever the legs do (a draw, a sheathe).</summary>
        ArmsAction,

        /// <summary>An action on the whole upper body, the legs still walking (a slash, a shot).</summary>
        UpperBodyAction,

        /// <summary>An action on the whole body (a death: it falls; one that stays at its end lies so, <see cref="MoveSettings.StaysAtEnd"/>).</summary>
        FullBodyAction,

        /// <summary>
        /// A reaction laid over whatever the body does (a blow taken): how its clip moves from its first frame is added on
        /// top, the whole body, so a swing, a walk or a block goes on beneath it.
        /// </summary>
        Reaction,
    }

    /// <summary>
    /// The moves a humanoid's animations are made of, each one file named after it in a move set
    /// (Content/Animations/{Set}/...), and what each is, by the folder it sits in:
    /// <list type="bullet">
    /// <item><see cref="LocomotionFolder"/>: the ground moves, blended by speed, their versions with weapons drawn and
    /// crouched, and the moves between standing and crouching.</item>
    /// <item><see cref="AirFolder"/>: the jump, the fall, the landing.</item>
    /// <item><see cref="StylesFolder"/>/{Style}: a combat style's moves; any other folder (Interact, Emotes, ...): more
    /// actions.</item>
    /// <item><see cref="BuiltFolder"/>: what the builder makes (the controller, its masks), never moves.</item>
    /// </list>
    /// Beside the sets, <see cref="ReferenceFolder"/> holds the skeleton the clips are read against
    /// (<see cref="AnimationImport.ReferenceRig"/>); a folder named with a leading underscore is never a set
    /// (<see cref="IsSet"/>).
    /// An action plays where its settings say (<see cref="MoveSettings.Plays"/>: on the arms alone, the whole body, or as
    /// a style's stance), else on the upper body; nothing is told from its name. A move's name is its own across the
    /// set's folders, since content names it so ("BowShot"). The import (<see cref="AnimationImport"/>) loops what
    /// repeats; the animator builder (<see cref="HumanoidAnimatorBuilder"/>) plays whichever of them are present.
    /// </summary>
    static class HumanoidMoves
    {
        public const string LocomotionFolder = "Locomotion";
        public const string AirFolder = "Air";
        public const string StylesFolder = "Styles";
        public const string BuiltFolder = "_Built";
        public const string ReferenceFolder = "_Reference";

        /// <summary>Whether a folder beside the sets, named <paramref name="folderName"/>, is a move set: the aside folders start with '_'.</summary>
        public static bool IsSet(string folderName) =>
            !string.IsNullOrEmpty(folderName) && !folderName.StartsWith("_", StringComparison.Ordinal);

        public const string Idle = "Idle";
        public const string Walk = Forge.Play.HumanoidAnimation.WalkMove;
        public const string Run = "Run";
        public const string Sprint = "Sprint";
        public const string Jump = "Jump";
        public const string Fall = "Fall";
        public const string Land = "Land";

        /// <summary>The ground moves, slowest first: the speed blend's order.</summary>
        public static readonly string[] Ground = { Idle, Walk, Run, Sprint };

        /// <summary>The ground moves with weapons drawn, in the same order; each falls back to its plain move when missing.</summary>
        public static readonly string[] DrawnGround = { Drawn(Idle), Drawn(Walk), Drawn(Run), Drawn(Sprint) };

        /// <summary>The ground moves crouched, in the same order; a set crouches when it has the crouched idle, with whichever others it has.</summary>
        public static readonly string[] CrouchedGround = { Crouched(Idle), Crouched(Walk), Crouched(Run), Crouched(Sprint) };

        /// <summary>The crouched ground moves with weapons drawn ("DrawnCrouchIdle"), in the same order; each falls back to its plain crouched move.</summary>
        public static readonly string[] DrawnCrouchedGround = { Drawn(Crouched(Idle)), Drawn(Crouched(Walk)), Drawn(Crouched(Run)), Drawn(Crouched(Sprint)) };

        /// <summary>
        /// Sidestepping to the right at a walk's pace (Locomotion/). The builder mirrors it for the left and speeds it up
        /// for the faster paces, and a ground state that has one blends by the way the body goes, not its speed alone.
        /// </summary>
        public const string Strafe = "Strafe";

        /// <summary>The sidestep with weapons drawn ("DrawnStrafe"): a fighter circling its foe; falls back to the plain one.</summary>
        public static readonly string DrawnStrafe = Drawn(Strafe);

        /// <summary>The sideways ground moves.</summary>
        public static readonly string[] Sideways = { Strafe, DrawnStrafe };

        /// <summary>Getting down into a crouch while standing still, played once (Locomotion/).</summary>
        public const string CrouchDown = "CrouchDown";

        /// <summary>Standing back up from a crouch while standing still, played once (Locomotion/).</summary>
        public const string StandUp = "StandUp";

        /// <summary>The ground moves played once, from one way of standing to another, rather than walked.</summary>
        public static readonly string[] PostureChanges = { CrouchDown, StandUp };

        /// <summary>The jump with weapons drawn (Air/); without it the plain jump plays drawn too. The fall and the landing are shared.</summary>
        public static readonly string DrawnJump = Drawn(Jump);

        /// <summary>The air moves.</summary>
        public static readonly string[] Air = { Jump, Fall, Land, DrawnJump };

        /// <summary>A ground move's drawn version ("DrawnRun").</summary>
        public static string Drawn(string move) => "Drawn" + move;

        /// <summary>A ground move's crouched version ("CrouchWalk").</summary>
        public static string Crouched(string move) => "Crouch" + move;

        /// <summary>
        /// What the move at <paramref name="path"/> (in its set, "Styles/Archery/BowShot.fbx", '/' or '\' between
        /// folders) is, by its folder and, for an action, what its <paramref name="settings"/> say
        /// (<see cref="MoveSettings.Plays"/>; a Locomotion or an Air move is its folder's whatever they say); null for
        /// none (outside any folder, or among what is built). Pure, so testable.
        /// </summary>
        public static MoveRole? RoleOf(string path, MoveSettings settings)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            var parts = path.Replace('\\', '/').Split('/');
            if (parts.Length < 2)
                return null;
            switch (parts[0])
            {
                case BuiltFolder:
                case ReferenceFolder: return null;
                case LocomotionFolder: return MoveRole.Ground;
                case AirFolder: return MoveRole.Air;
                default: return (settings ?? MoveSettings.None).Plays ?? MoveRole.UpperBodyAction;
            }
        }

        /// <summary>What the move at <paramref name="path"/> is when it has no settings (<see cref="MoveSettings.None"/>).</summary>
        public static MoveRole? RoleOf(string path) => RoleOf(path, MoveSettings.None);

        /// <summary>
        /// Whether a move of <paramref name="role"/>, whose folder decides what it is, has <paramref name="settings"/> that
        /// try to say otherwise ("plays" on a Locomotion or an Air move): it plays as its folder says, and they are
        /// pointless.
        /// </summary>
        public static bool IgnoresPlays(MoveRole role, MoveSettings settings) =>
            (role == MoveRole.Ground || role == MoveRole.Air) && settings?.Plays != null;

        /// <summary>Whether a move of <paramref name="role"/> named <paramref name="move"/> repeats while it lasts (standing, walking, falling) rather than playing once.</summary>
        public static bool Loops(MoveRole role, string move) =>
            (role == MoveRole.Ground && Array.IndexOf(PostureChanges, move) < 0) || role == MoveRole.Stance || (role == MoveRole.Air && move == Fall);

        /// <summary>
        /// Whether a move of <paramref name="role"/> named <paramref name="move"/> lifts the body off the ground (a jump).
        /// The body's own rise already does that, so the clip's rise is left out of its pose; other moves keep theirs.
        /// </summary>
        public static bool Lifts(MoveRole role, string move) => role == MoveRole.Air && (move == Jump || move == DrawnJump);

        /// <summary>
        /// Where a move named <paramref name="move"/> of <paramref name="role"/> ought to be, when the builder only looks
        /// for it elsewhere: a known ground or air move outside its folder; else null.
        /// </summary>
        public static string Misplaced(string move, MoveRole role)
        {
            if (role != MoveRole.Ground && (Array.IndexOf(Ground, move) >= 0 || Array.IndexOf(DrawnGround, move) >= 0 ||
                    Array.IndexOf(CrouchedGround, move) >= 0 || Array.IndexOf(DrawnCrouchedGround, move) >= 0 ||
                    Array.IndexOf(Sideways, move) >= 0 || Array.IndexOf(PostureChanges, move) >= 0))
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
    }
}
