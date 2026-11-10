using System;
using System.Collections.Generic;
using System.IO;
using Forge.Play;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// How a move holds, from the optional {Move}.json beside its clip: where what each hand holds points in it
    /// (<c>{ "rightHand": { "points": [0, 0, 1] } }</c>: the grip's +Y, a sword's blade, straight ahead in the body's
    /// frame, x right, y up, z forward; "leftHand" too), which the hand is turned the least to do on top of the clip's
    /// pose (<see cref="HandAim"/>): a Mixamo move is animated with nothing in the hand, and holds our sword as it may.
    /// Every clip carries the six values as curves driving the animator's parameters
    /// (<see cref="HumanoidAnimation.HandPoints"/>; zero where the move says nothing), so the animator blends them as it
    /// blends the moves: a stance's aim fades out as it walks or strikes, and comes back as the strike ends.
    /// </summary>
    static class MoveSettings
    {
        /// <summary>The .json beside the clip at <paramref name="clipPath"/>, there or not.</summary>
        public static string PathFor(string clipPath) => Path.ChangeExtension(clipPath, ".json");

        /// <summary>The curves the clip at <paramref name="clipPath"/> carries, from its .json when it has one, else all zero.</summary>
        public static IReadOnlyList<(string Name, float Value)> Read(string clipPath)
        {
            var path = PathFor(clipPath);
            return Parse(File.Exists(path) ? File.ReadAllText(path) : null, path);
        }

        /// <summary>
        /// The curves <paramref name="json"/> (a move's .json; null for none) asks for: each hand's three, right then
        /// left, each axis of where it points, zero where it says nothing. A hand's "points" that is not three numbers
        /// is refused, naming <paramref name="path"/>.
        /// </summary>
        public static IReadOnlyList<(string Name, float Value)> Parse(string json, string path)
        {
            var settings = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<Json>(json);
            var curves = new List<(string, float)>(6);
            Add(curves, true, Points(path, "rightHand", settings?.rightHand));
            Add(curves, false, Points(path, "leftHand", settings?.leftHand));
            return curves;
        }

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
