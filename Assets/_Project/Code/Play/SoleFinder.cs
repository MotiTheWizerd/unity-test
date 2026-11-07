using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// How far above its feet a character's soles are as it stands, read off its own standing clip rather than set by
    /// hand: played on a hidden copy (<see cref="ClipSampler"/>), the lowest point of its posed meshes over the clip.
    /// Retargeted clips can carry the hips a little high for a body (a Mixamo idle on the Viking: 11 cm), which would
    /// leave it standing in the air with its shadow apart from it; the look sinks by this much
    /// (<see cref="CharacterModel"/>). Measured once per character and clip, then remembered.
    /// </summary>
    public static class SoleFinder
    {
        // Poses sampled across the clip; an idle sways little.
        const int Samples = 4;

        static readonly Dictionary<(GameObject, AnimationClip), float> Found = new Dictionary<(GameObject, AnimationClip), float>();

        /// <summary>
        /// How far above its feet (its root) <paramref name="character"/>'s soles stand in <paramref name="clip"/>, meters;
        /// false when it can't tell (no clip, no humanoid, no mesh).
        /// </summary>
        public static bool TryFind(GameObject character, AnimationClip clip, out float height)
        {
            height = 0f;
            if (character == null || clip == null)
                return false;
            var key = (character, clip);
            if (!Found.TryGetValue(key, out height))
            {
                height = Measure(character, clip);
                Found.Add(key, height);
            }

            return !float.IsNaN(height);
        }

        static float Measure(GameObject character, AnimationClip clip)
        {
            using (var sampler = ClipSampler.TryCreate(character, clip))
            {
                if (sampler == null)
                    return float.NaN;
                var lowest = float.PositiveInfinity;
                for (var i = 0; i < Samples; i++)
                {
                    sampler.PoseAt(clip.length * i / Samples);
                    foreach (var point in sampler.MeshPoints())
                        lowest = Mathf.Min(lowest, point.y);
                }

                return float.IsInfinity(lowest) ? float.NaN : lowest;
            }
        }
    }
}
