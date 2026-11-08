using System;
using System.Collections.Generic;
using Forge.Game.Combat;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// How a bow's draw plays, read off its own clip as <see cref="SwingFinder"/> reads a cut: played on a hidden copy
    /// (<see cref="ClipSampler"/>), the arrow is on the string where the drawing hand comes nearest the bow hand (it
    /// brings the arrow to the bow), and the bow is fully drawn where, after that, the hands are farthest apart. That run
    /// is the move's window: from it a release looses, at its end the draw is held. The move lasts the clip. Measured once
    /// per character and clip, then remembered.
    /// </summary>
    public static class ShotFinder
    {
        // Samples across the clip: a 1 s clip at 30 fps has 31 frames.
        const int Samples = 60;

        static readonly Dictionary<(GameObject, AnimationClip), MoveTiming?> Found = new Dictionary<(GameObject, AnimationClip), MoveTiming?>();

        /// <summary>How <paramref name="clip"/> (a draw) plays on <paramref name="character"/>; false when it can't tell (no clip, no hands).</summary>
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
        /// The draw's window in <paramref name="apart"/> (how far apart the hands are at evenly spaced samples, the first
        /// at the start, the last at the end): from the nearest they come (nocking) to the farthest after it (full draw),
        /// as fractions of the whole. Pure, so testable.
        /// </summary>
        public static (float From, float To) Window(IReadOnlyList<float> apart)
        {
            if (apart == null)
                throw new ArgumentNullException(nameof(apart));
            if (apart.Count < 2)
                return (0f, 1f);
            var nearest = 0;
            for (var i = 1; i < apart.Count; i++)
            {
                if (apart[i] < apart[nearest])
                    nearest = i;
            }

            var farthest = nearest;
            for (var i = nearest + 1; i < apart.Count; i++)
            {
                if (apart[i] >= apart[farthest])
                    farthest = i;
            }

            var last = apart.Count - 1f;
            return (nearest / last, farthest / last);
        }

        static MoveTiming? Measure(GameObject character, AnimationClip clip)
        {
            if (!(clip.length > 0f))
                return null;
            using (var sampler = ClipSampler.TryCreate(character, clip))
            {
                if (sampler == null)
                    return null;
                var apart = new float[Samples];
                var step = clip.length / (Samples - 1);
                for (var i = 0; i < Samples; i++)
                {
                    if (!sampler.TryGripAt(i * step, true, out var right) || !HumanoidHands.TryGrip(sampler.Animator, false, out var left))
                        return null;
                    apart[i] = Vector3.Distance(right.position, left.position);
                }

                var (from, to) = Window(apart);
                return new MoveTiming(clip.length, from, to);
            }
        }
    }
}
