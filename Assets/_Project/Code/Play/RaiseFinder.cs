using System;
using System.Collections.Generic;
using Forge.Game.Combat;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// How a block plays (<see cref="MoveMeasure.Raise"/>), read off its own clip as <see cref="ShotFinder"/> reads a
    /// draw: played on a hidden copy (<see cref="ClipSampler"/>), the guard hand is the one that has gone farthest from
    /// where it began by the end (the shield's), and the guard is up from where that hand has come
    /// <see cref="UpAt"/> of its way. That is the move's window: from it blows are blocked, and at its end, the clip's,
    /// the block is held. Measured once per character and clip, then remembered.
    /// </summary>
    public static class RaiseFinder
    {
        /// <summary>How much of its way the guard hand has come when the guard counts as up.</summary>
        public const float UpAt = 0.8f;

        // Samples across the clip: a 1 s clip at 30 fps has 31 frames.
        const int Samples = 60;

        static readonly Dictionary<(GameObject, AnimationClip), MoveTiming?> Found = new Dictionary<(GameObject, AnimationClip), MoveTiming?>();

        /// <summary>How <paramref name="clip"/> (a block) plays on <paramref name="character"/>; false when it can't tell (no clip, no hands).</summary>
        public static bool TryFind(GameObject character, AnimationClip clip, out MoveTiming timing)
        {
            timing = default;
            if (character == null || clip == null)
                return false;
            var key = (character, clip);
            if (!Found.TryGetValue(key, out var found))
            {
                found = Measure(character, clip);
                Found.Add(key, found);
            }

            if (found == null)
                return false;
            timing = found.Value;
            return true;
        }

        /// <summary>
        /// Where the guard is up in <paramref name="moved"/> (how far the guard hand is from where it began, at evenly
        /// spaced samples, the first at the start, the last at the end): the first sample it has come <see cref="UpAt"/>
        /// of its last distance, as a fraction of the whole; 1 when it never moves. Pure, so testable.
        /// </summary>
        public static float Up(IReadOnlyList<float> moved)
        {
            if (moved == null)
                throw new ArgumentNullException(nameof(moved));
            if (moved.Count < 2 || !(moved[moved.Count - 1] > 0f))
                return 1f;
            var up = moved[moved.Count - 1] * UpAt;
            for (var i = 0; i < moved.Count; i++)
            {
                if (moved[i] >= up)
                    return i / (moved.Count - 1f);
            }

            return 1f;
        }

        static MoveTiming? Measure(GameObject character, AnimationClip clip)
        {
            if (!(clip.length > 0f))
                return null;
            using (var sampler = ClipSampler.TryCreate(character, clip))
            {
                if (sampler == null)
                    return null;
                var right = new float[Samples];
                var left = new float[Samples];
                var step = clip.length / (Samples - 1);
                Vector3 rightFrom = default, leftFrom = default;
                for (var i = 0; i < Samples; i++)
                {
                    if (!sampler.TryGripAt(i * step, true, out var r) || !HumanoidHands.TryGrip(sampler.Animator, false, out var l))
                        return null;
                    if (i == 0)
                    {
                        rightFrom = r.position;
                        leftFrom = l.position;
                    }

                    right[i] = Vector3.Distance(r.position, rightFrom);
                    left[i] = Vector3.Distance(l.position, leftFrom);
                }

                var guard = left[Samples - 1] >= right[Samples - 1] ? left : right;
                return new MoveTiming(clip.length, Up(guard), 1f);
            }
        }
    }
}
