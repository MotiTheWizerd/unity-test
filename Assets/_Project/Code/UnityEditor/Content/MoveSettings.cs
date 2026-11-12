using System;
using System.Collections.Generic;
using System.IO;
using Forge.Play;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// What a move's optional {Move}.json beside its clip says of it, every part optional, so a clip without one plays as
    /// its folder says (<see cref="HumanoidMoves.RoleOf"/>); a move's name says nothing of it but its name:
    /// <list type="bullet">
    /// <item>"plays": where an action plays (<see cref="Plays"/>): "arms" alone, whatever the legs do (a draw, a sheathe);
    /// "upperBody", the legs walking on beneath (a slash: an action that doesn't say); "wholeBody" (a death); or "stance",
    /// a combat style's drawn idle, which its drawn ground moves stand in ("BowIdle"). A Locomotion or an Air move plays
    /// as its folder says.</item>
    /// <item>"staysAtEnd": a whole-body action played through stays at its end (a death: it lies as it fell) until let go
    /// (<see cref="StaysAtEnd"/>).</item>
    /// <item>"rightHand", "leftHand": where what the hand holds points in it (<c>{ "points": [0, 0, 1] }</c>: the grip's
    /// +Y, a sword's blade, straight ahead in the body's frame, x right, y up, z forward), which the hand is turned the
    /// least to do on top of the clip's pose (<see cref="HandAim"/>): a Mixamo move is animated with nothing in the hand,
    /// and holds our sword as it may. Every clip carries the six values as curves driving the animator's parameters
    /// (<see cref="Curves"/>, <see cref="HumanoidAnimation.HandPoints"/>; zero where the move says nothing), so the
    /// animator blends them as it blends the moves: a stance's aim fades out as it walks or strikes, and comes back as
    /// the strike ends.</item>
    /// </list>
    /// The import reads it (the hands' curves, whether the clip loops) and so does the animator builder (where it plays).
    /// </summary>
    sealed class MoveSettings
    {
        /// <summary>A move's settings when it has no .json: its folder's way, its hands pointing nowhere.</summary>
        public static readonly MoveSettings None = new MoveSettings(null, false, Vector3.zero, Vector3.zero);

        static readonly Dictionary<string, MoveRole> PlaysByName = new Dictionary<string, MoveRole>(StringComparer.Ordinal)
        {
            { "arms", MoveRole.ArmsAction },
            { "upperBody", MoveRole.UpperBodyAction },
            { "wholeBody", MoveRole.FullBodyAction },
            { "stance", MoveRole.Stance },
        };

        MoveSettings(MoveRole? plays, bool staysAtEnd, Vector3 rightHand, Vector3 leftHand)
        {
            Plays = plays;
            StaysAtEnd = staysAtEnd;
            RightHand = rightHand;
            LeftHand = leftHand;
        }

        /// <summary>Where it plays when it is an action, or as a stance; null: its folder says.</summary>
        public MoveRole? Plays { get; }

        /// <summary>Whether, played through on the whole body, it stays at its end until let go (a death).</summary>
        public bool StaysAtEnd { get; }

        /// <summary>Where what the right hand holds points in the move (the body's frame); zero: it doesn't say.</summary>
        public Vector3 RightHand { get; }

        /// <summary>Where what the left hand holds points in the move; zero: it doesn't say.</summary>
        public Vector3 LeftHand { get; }

        /// <summary>The curves the clip carries: each hand's three, right then left, each axis of where it points.</summary>
        public IReadOnlyList<(string Name, float Value)> Curves
        {
            get
            {
                var curves = new List<(string, float)>(6);
                Add(curves, true, RightHand);
                Add(curves, false, LeftHand);
                return curves;
            }
        }

        /// <summary>These settings with each hand pointing as given (zero: it doesn't say), the rest as they are.</summary>
        public MoveSettings WithHands(Vector3 right, Vector3 left) => new MoveSettings(Plays, StaysAtEnd, right, left);

        /// <summary>The .json beside the clip at <paramref name="clipPath"/>, there or not.</summary>
        public static string PathFor(string clipPath) => Path.ChangeExtension(clipPath, ".json");

        /// <summary>The settings of the clip at <paramref name="clipPath"/>, from its .json when it has one, else <see cref="None"/>.</summary>
        public static MoveSettings Read(string clipPath)
        {
            var path = PathFor(clipPath);
            return Parse(File.Exists(path) ? File.ReadAllText(path) : null, path);
        }

        /// <summary>
        /// The settings <paramref name="json"/> (a move's .json; null for none) says. What it can't mean is refused, naming
        /// <paramref name="path"/>: a "plays" that is no way of playing, staying at its end anywhere but on the whole
        /// body, a hand's "points" that is not three numbers.
        /// </summary>
        public static MoveSettings Parse(string json, string path)
        {
            var settings = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<Json>(json);
            if (settings == null)
                return None;
            MoveRole? plays = null;
            if (!string.IsNullOrEmpty(settings.plays))
            {
                if (!PlaysByName.TryGetValue(settings.plays, out var role))
                    throw new InvalidOperationException($"{path}: \"plays\" is one of {string.Join(", ", PlaysByName.Keys)}; it says \"{settings.plays}\".");
                plays = role;
            }

            if (settings.staysAtEnd && plays != MoveRole.FullBodyAction)
                throw new InvalidOperationException($"{path}: only a move that \"plays\": \"wholeBody\" \"staysAtEnd\".");
            return new MoveSettings(plays, settings.staysAtEnd, Points(path, "rightHand", settings.rightHand), Points(path, "leftHand", settings.leftHand));
        }

        /// <summary>The .json saying what these settings say, and nothing they leave unsaid, as <see cref="Parse"/> reads it back.</summary>
        public string Write()
        {
            var parts = new List<string>(4);
            if (Plays.HasValue)
            {
                foreach (var named in PlaysByName)
                {
                    if (named.Value == Plays.Value)
                        parts.Add($"\"plays\": \"{named.Key}\"");
                }
            }

            if (StaysAtEnd)
                parts.Add("\"staysAtEnd\": true");
            if (RightHand != Vector3.zero)
                parts.Add($"\"rightHand\": {{ \"points\": [{Number(RightHand.x)}, {Number(RightHand.y)}, {Number(RightHand.z)}] }}");
            if (LeftHand != Vector3.zero)
                parts.Add($"\"leftHand\": {{ \"points\": [{Number(LeftHand.x)}, {Number(LeftHand.y)}, {Number(LeftHand.z)}] }}");
            return "{ " + string.Join(", ", parts) + " }\n";
        }

        static string Number(float value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        static void Add(List<(string, float)> curves, bool right, Vector3 points)
        {
            for (var axis = 0; axis < 3; axis++)
                curves.Add((HumanoidAnimation.HandPoints(right, axis), points[axis]));
        }

        // Where the hand points, or nothing (zero) when the move doesn't say.
        static Vector3 Points(string path, string hand, Hand settings)
        {
            var points = settings?.points;
            if (points == null || points.Length == 0)
                return Vector3.zero;
            if (points.Length != 3)
                throw new InvalidOperationException($"{path}: \"{hand}\".\"points\" is a direction, three numbers (x right, y up, z forward); it has {points.Length}.");
            return new Vector3(points[0], points[1], points[2]);
        }

        [Serializable]
        sealed class Json
        {
            public string plays;
            public bool staysAtEnd;
            public Hand rightHand;
            public Hand leftHand;
        }

        [Serializable]
        sealed class Hand
        {
            public float[] points;
        }
    }
}
