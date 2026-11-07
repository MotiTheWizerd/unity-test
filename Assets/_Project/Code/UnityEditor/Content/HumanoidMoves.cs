using System;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The moves a humanoid's animations are made of, each one file in a move set's folder named after it
    /// (Content/Animations/{Set}/{Move}.fbx): the ground moves blend by speed, the air moves play on top. The import
    /// (<see cref="AnimationImport"/>) loops what repeats; the animator builder (<see cref="HumanoidAnimatorBuilder"/>)
    /// plays whichever of them are present.
    /// </summary>
    static class HumanoidMoves
    {
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

        /// <summary>A ground move's drawn version ("DrawnRun").</summary>
        public static string Drawn(string move) => "Drawn" + move;

        /// <summary>
        /// Whether <paramref name="move"/> is an action: anything beyond the ground and air moves (a draw, a sheathe, a
        /// combat style's slash), played on the arms by a trigger of its name, so a clip dropped in is playable by name.
        /// </summary>
        public static bool IsAction(string move) =>
            Array.IndexOf(Ground, move) < 0 && Array.IndexOf(DrawnGround, move) < 0 &&
            move != Jump && move != Fall && move != Land;

        /// <summary>
        /// Whether action <paramref name="move"/> plays on the arms alone (drawing and sheathing: the body walks or runs on
        /// unchanged) rather than the whole upper body (a slash: the torso turns into the cut, the legs still walk).
        /// </summary>
        public static bool ArmsOnly(string move) => move == Draw || move == Sheathe;

        /// <summary>Whether <paramref name="move"/> repeats while it lasts (standing, walking, falling) rather than playing once (a jump, a landing).</summary>
        public static bool Loops(string move) =>
            Array.IndexOf(Ground, move) >= 0 || Array.IndexOf(DrawnGround, move) >= 0 || string.Equals(move, Fall, StringComparison.Ordinal);

        /// <summary>
        /// Whether <paramref name="move"/> lifts the body off the ground (a jump). The body's own rise already does that,
        /// so the clip's rise is left out of its pose; other moves keep theirs (a stride's bob, a landing's crouch).
        /// </summary>
        public static bool Lifts(string move) => string.Equals(move, Jump, StringComparison.Ordinal);
    }
}
