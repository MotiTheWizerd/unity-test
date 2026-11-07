using System;
using System.Collections.Generic;
using Forge.Game.Combat;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// How a character's combat move plays, read off its own clip rather than timed by hand: played on a hidden copy
    /// (<see cref="ClipSampler"/>), each hand holds a blade <see cref="BladeLength"/> long out of its grip, the hand whose
    /// blade tip moves fastest is the one swinging, and the blow can land while that tip moves at least
    /// <see cref="WindowSpeed"/> of its fastest, around its fastest moment (<see cref="Window"/>). The tip, not the hand:
    /// a wind-up lifts the hand fast, the cut sweeps the blade. The move lasts the clip. Measured once per character and
    /// clip, then remembered.
    /// </summary>
    public static class SwingFinder
    {
        /// <summary>
        /// The part of its top speed a swinging blade's tip keeps up through the hit window: a third, so the whole cut
        /// counts, not only its fastest instant (a cut slows a little as it comes round in front).
        /// </summary>
        public const float WindowSpeed = 0.3f;

        /// <summary>Meters of the blade measured out of the grip: a sword's, whatever the weapon is (the clip is the same).</summary>
        public const float BladeLength = 0.9f;

        // Samples across the clip: a 1.2 s clip at 30 fps has 36 frames.
        const int Samples = 60;

        static readonly Dictionary<(GameObject, AnimationClip), MoveTiming?> Found = new Dictionary<(GameObject, AnimationClip), MoveTiming?>();

        /// <summary>How <paramref name="clip"/> plays on <paramref name="character"/>; false when it can't tell (no clip, no hands).</summary>
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
        /// The hit window in <paramref name="speeds"/> (a hand's speed between evenly spaced samples, the first between
        /// the first two): the run of samples around the fastest that keep <paramref name="part"/> of its speed, as
        /// fractions of the whole (from the start of its first step to the end of its last). Pure, so testable.
        /// </summary>
        public static (float From, float To) Window(IReadOnlyList<float> speeds, float part = WindowSpeed)
        {
            if (speeds == null)
                throw new ArgumentNullException(nameof(speeds));
            if (speeds.Count == 0)
                return (0f, 1f);
            var peak = 0;
            for (var i = 1; i < speeds.Count; i++)
            {
                if (speeds[i] > speeds[peak])
                    peak = i;
            }

            var floor = speeds[peak] * part;
            var first = peak;
            while (first > 0 && speeds[first - 1] >= floor)
                first--;
            var last = peak;
            while (last < speeds.Count - 1 && speeds[last + 1] >= floor)
                last++;
            return ((float)first / speeds.Count, (float)(last + 1) / speeds.Count);
        }

        static MoveTiming? Measure(GameObject character, AnimationClip clip)
        {
            if (!(clip.length > 0f))
                return null;
            using (var sampler = ClipSampler.TryCreate(character, clip))
            {
                if (sampler == null || !TrySpeeds(sampler, true, out var right) || !TrySpeeds(sampler, false, out var left))
                    return null;
                var (from, to) = Window(Peak(right) >= Peak(left) ? right : left);
                return new MoveTiming(clip.length, from, to);
            }
        }

        // How fast the tip of a blade held in the hand moves between each pair of samples, meters a second, seen from the
        // chest: actions play on the arms only (HumanoidAnimation's arms layer), so the clip's own twist of the body is
        // not part of the swing the game shows.
        static bool TrySpeeds(ClipSampler sampler, bool right, out float[] speeds)
        {
            speeds = new float[Samples - 1];
            var step = sampler.Clip.length / (Samples - 1);
            var chest = sampler.Animator.GetBoneTransform(HumanBodyBones.Chest) ?? sampler.Animator.GetBoneTransform(HumanBodyBones.Spine) ?? sampler.Root;
            Vector3 last = default;
            for (var i = 0; i < Samples; i++)
            {
                if (!sampler.TryGripAt(i * step, right, out var grip))
                    return false;
                var now = chest.InverseTransformPoint(grip.position + grip.rotation * Vector3.up * BladeLength) * chest.lossyScale.x;
                if (i > 0)
                    speeds[i - 1] = Vector3.Distance(now, last) / step;
                last = now;
            }

            return true;
        }

        static float Peak(float[] speeds)
        {
            var peak = 0f;
            foreach (var speed in speeds)
                peak = Mathf.Max(peak, speed);
            return peak;
        }
    }
}
