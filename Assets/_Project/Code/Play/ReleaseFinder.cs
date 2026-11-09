using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Where a bow's release (<see cref="Data.Records.CombatMove.Release"/>) plays from, read off its own clip as
    /// <see cref="ShotFinder"/> reads the draw: played on a hidden copy (<see cref="ClipSampler"/>), the string goes where
    /// the hands part fastest (the drawing hand flicking back off it). What comes before that is the draw still held, so
    /// the release starts <see cref="Lead"/> seconds short of it, blended in from the held draw meanwhile, and the
    /// string goes just as the blend is done. Measured once per character and clip, then remembered.
    /// </summary>
    public static class ReleaseFinder
    {
        /// <summary>Seconds before the string goes that the release starts, blended in from the held draw over them.</summary>
        public const float Lead = 0.06f;

        // Samples across the clip: a 1 s clip at 30 fps has 31 frames.
        const int Samples = 60;

        static readonly Dictionary<(GameObject, AnimationClip), float?> Found = new Dictionary<(GameObject, AnimationClip), float?>();

        /// <summary>
        /// Where <paramref name="clip"/> (a release) starts on <paramref name="character"/>, a fraction of it; false when
        /// it can't tell (no clip, no hands).
        /// </summary>
        public static bool TryFind(GameObject character, AnimationClip clip, out float from)
        {
            from = 0f;
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
            from = found.Value;
            return true;
        }

        /// <summary>
        /// Where the string goes in <paramref name="apart"/> (how far apart the hands are at evenly spaced samples, the
        /// first at the start, the last at the end): the sample the hands part fastest from, as a fraction of the whole;
        /// 0 when they never part. Pure, so testable.
        /// </summary>
        public static float Moment(IReadOnlyList<float> apart)
        {
            if (apart == null)
                throw new ArgumentNullException(nameof(apart));
            var fastest = -1;
            var most = 0f;
            for (var i = 0; i + 1 < apart.Count; i++)
            {
                var parting = apart[i + 1] - apart[i];
                if (parting > most)
                {
                    most = parting;
                    fastest = i;
                }
            }

            return fastest < 0 ? 0f : fastest / (apart.Count - 1f);
        }

        static float? Measure(GameObject character, AnimationClip clip)
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

                return Mathf.Max(0f, Moment(apart) - Lead / clip.length);
            }
        }
    }
}
