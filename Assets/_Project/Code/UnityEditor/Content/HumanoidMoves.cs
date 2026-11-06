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

        /// <summary>The ground moves, slowest first: the speed blend's order.</summary>
        public static readonly string[] Ground = { Idle, Walk, Run, Sprint };

        /// <summary>Whether <paramref name="move"/> repeats while it lasts (standing, walking, falling) rather than playing once (a jump, a landing).</summary>
        public static bool Loops(string move) =>
            Array.IndexOf(Ground, move) >= 0 || string.Equals(move, Fall, StringComparison.Ordinal);

        /// <summary>
        /// Whether <paramref name="move"/> lifts the body off the ground (a jump). The body's own rise already does that,
        /// so the clip's rise is left out of its pose; other moves keep theirs (a stride's bob, a landing's crouch).
        /// </summary>
        public static bool Lifts(string move) => string.Equals(move, Jump, StringComparison.Ordinal);
    }
}
