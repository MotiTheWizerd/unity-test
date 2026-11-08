using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Where a character carries a long thing on its back (a bow, as in Skyrim), read off the character itself rather than
    /// placed by hand: played in its idle on a hidden copy (<see cref="ClipSampler"/>), the line from one hip up across the
    /// back to the shoulder of the hand that holds it (a bow held in the left: right hip to left shoulder, so that hand
    /// reaches it over its own shoulder), its middle at the height of the chest bone (the upper back, below the neck),
    /// leaning on the back as a straight edge would (<see cref="TryRest"/>: on the body beside that line, clothes and
    /// all, so a hunched back holds it out above and it lies in below). Relative to the chest, so it moves with the torso. And when
    /// in a draw or a put-away the hand is at it (<see cref="TryFindReach"/>): the moment the thing changes place.
    /// Measured once per character, clip and hand, then remembered.
    /// </summary>
    public static class BackFinder
    {
        // Samples across a draw: a second's clip at 30 fps has 30 frames.
        const int Samples = 60;

        /// <summary>How far what is carried on the back stands off it, meters: a strap's thickness, so it lies on the back without passing into it.</summary>
        public const float Strap = 0.01f;

        static readonly Dictionary<(GameObject, AnimationClip, bool), Pose?> Spots = new Dictionary<(GameObject, AnimationClip, bool), Pose?>();
        static readonly Dictionary<(GameObject, AnimationClip, AnimationClip, bool), float?> Reaches = new Dictionary<(GameObject, AnimationClip, AnimationClip, bool), float?>();

        /// <summary>
        /// The spot on <paramref name="character"/>'s back for what its right or left hand holds, as it stands in
        /// <paramref name="idle"/>, relative to its chest bone (<see cref="Chest"/>): +Y up the line to that hand's
        /// shoulder, leaning as the back does, +Z out of it. False when it can't tell (no clip, no humanoid, no mesh).
        /// </summary>
        public static bool TryFind(GameObject character, AnimationClip idle, bool right, out Pose inChest)
        {
            inChest = default;
            if (character == null || idle == null)
                return false;
            var key = (character, idle, right);
            if (!Spots.TryGetValue(key, out var found))
            {
                found = Measure(character, idle, right);
                Spots.Add(key, found);
            }

            inChest = found ?? default;
            return found.HasValue;
        }

        /// <summary>
        /// How far into <paramref name="clip"/> (a draw or a put-away) the right or left hand comes nearest its spot on the
        /// back, 0 to 1: when what it holds goes from the back to the hand, or back. False when it can't tell.
        /// </summary>
        public static bool TryFindReach(GameObject character, AnimationClip clip, AnimationClip idle, bool right, out float moment)
        {
            moment = 1f;
            if (clip == null || !(clip.length > 0f) || !TryFind(character, idle, right, out var spot))
                return false;
            var key = (character, clip, idle, right);
            if (!Reaches.TryGetValue(key, out var found))
            {
                found = MeasureReach(character, clip, right, spot);
                Reaches.Add(key, found);
            }

            moment = found ?? 1f;
            return found.HasValue;
        }

        /// <summary>The bone a back spot rides on: the upper chest, else the chest, else the spine; null without one.</summary>
        public static Transform Chest(Animator humanoid)
        {
            if (humanoid == null)
                return null;
            var chest = humanoid.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null)
                chest = humanoid.GetBoneTransform(HumanBodyBones.Chest);
            return chest != null ? chest : humanoid.GetBoneTransform(HumanBodyBones.Spine);
        }

        static Pose? Measure(GameObject character, AnimationClip idle, bool right)
        {
            using (var sampler = ClipSampler.TryCreate(character, idle))
            {
                if (sampler == null)
                    return null;
                sampler.PoseAt(0f);
                var animator = sampler.Animator;
                var chest = Chest(animator);
                var hip = animator.GetBoneTransform(right ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                var shoulder = animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
                if (chest == null || hip == null || shoulder == null)
                    return null;

                // In the copy's frame, which faces +Z: the line across the back, seen from behind.
                var root = sampler.Root;
                var from = root.InverseTransformPoint(hip.position);
                var to = root.InverseTransformPoint(shoulder.position);
                var across = to - from;
                across.z = 0f;
                if (across.sqrMagnitude < 1e-6f || !(to.y > from.y))
                    return null;

                // Its middle on that line at the chest bone's height: the upper back, below the neck.
                var height = root.InverseTransformPoint(chest.position).y;
                var middle = Vector3.Lerp(from, to, Mathf.Clamp01((height - from.y) / (to.y - from.y)));

                // Leaning on the back as a stick leans on it: the body's back along the line, and the straight edge
                // resting on it, so a hunched upper back holds it out above and it lies in against the lower back.
                var line = across.normalized;
                // From the shoulder down past the hip by half as far again, where the lower limb lies on the seat.
                var lowest = Vector3.Dot(from - middle, line) - across.magnitude * 0.5f;
                var profile = BackProfile(sampler.MeshPoints(), middle, line, lowest, Vector3.Dot(to - middle, line));
                if (!TryRest(profile, out var depth, out var slope))
                    return null;
                var up = (line + Vector3.back * slope).normalized;
                var outward = (Vector3.back - up * Vector3.Dot(Vector3.back, up)).normalized;
                var spot = new Pose(new Vector3(middle.x, middle.y, -depth), Quaternion.LookRotation(outward, up));
                var position = root.TransformPoint(spot.position);
                var rotation = root.rotation * spot.rotation;
                var inverse = Quaternion.Inverse(chest.rotation);
                return new Pose(inverse * (position - chest.position), inverse * rotation);
            }
        }

        // How far back the body reaches beside the line (within a blade's reach of it, seen from behind), every step
        // along it from `from` to `to` (meters along the line from the middle): (along, depth behind the root) pairs.
        static List<Vector2> BackProfile(List<Vector3> points, Vector3 middle, Vector3 line, float from, float to)
        {
            var steps = Mathf.Max(1, Mathf.CeilToInt((to - from) / SheathFinder.BodyStep));
            var deepest = new float[steps + 1];
            for (var i = 0; i <= steps; i++)
                deepest[i] = float.NegativeInfinity;
            foreach (var point in points)
            {
                var offset = point - middle;
                offset.z = 0f;
                var along = Vector3.Dot(offset, line);
                if (along < from || along > to || (offset - line * along).sqrMagnitude > SheathFinder.BodyReach * SheathFinder.BodyReach)
                    continue;
                var step = Mathf.RoundToInt((along - from) / SheathFinder.BodyStep);
                deepest[step] = Mathf.Max(deepest[step], -point.z);
            }

            var profile = new List<Vector2>();
            for (var i = 0; i <= steps; i++)
            {
                if (!float.IsNegativeInfinity(deepest[i]))
                    profile.Add(new Vector2(from + i * SheathFinder.BodyStep, deepest[i]));
            }

            return profile;
        }

        /// <summary>
        /// The straight edge resting on a profile from outside (each point: along the line, depth behind the body's
        /// front): depth = <paramref name="depth"/> + <paramref name="slope"/> × along, behind every point and as near the
        /// body under its middle as that allows (on the two points it rests on there); when it rests on a point right
        /// under its middle and could rock between two ways, midway between them. False with no profile.
        /// </summary>
        public static bool TryRest(IReadOnlyList<Vector2> profile, out float depth, out float slope)
        {
            depth = slope = 0f;
            if (profile == null || profile.Count == 0)
                return false;

            // Level on the farthest point; then any two points on either side of the middle it can rest on, behind all
            // the others, nearer the body under the middle.
            depth = float.NegativeInfinity;
            foreach (var point in profile)
                depth = Mathf.Max(depth, point.y);
            foreach (var below in profile)
            {
                if (below.x > 0f)
                    continue;
                foreach (var above in profile)
                {
                    if (above.x < 0f || above.x - below.x < 1e-4f)
                        continue;
                    var k = (above.y - below.y) / (above.x - below.x);
                    var atMiddle = below.y - k * below.x;
                    if (atMiddle < depth && RestsOnAll(profile, atMiddle, k))
                        depth = atMiddle;
                }
            }

            // The slopes it can take there: no steeper than the points above allow, no shallower than those below.
            float least = float.NegativeInfinity, most = float.PositiveInfinity;
            foreach (var point in profile)
            {
                if (point.x > 1e-4f)
                    least = Mathf.Max(least, (point.y - depth) / point.x);
                else if (point.x < -1e-4f)
                    most = Mathf.Min(most, (point.y - depth) / point.x);
            }

            slope = !float.IsInfinity(least) && !float.IsInfinity(most) ? (least + most) * 0.5f
                : !float.IsInfinity(least) ? least
                : !float.IsInfinity(most) ? most
                : 0f;
            return true;
        }

        static bool RestsOnAll(IReadOnlyList<Vector2> profile, float depth, float slope)
        {
            foreach (var point in profile)
            {
                if (point.y > depth + slope * point.x + 1e-4f)
                    return false;
            }

            return true;
        }

        static float? MeasureReach(GameObject character, AnimationClip clip, bool right, Pose spot)
        {
            using (var sampler = ClipSampler.TryCreate(character, clip))
            {
                var chest = sampler != null ? Chest(sampler.Animator) : null;
                if (chest == null)
                    return null;
                var nearest = float.PositiveInfinity;
                var moment = 1f;
                for (var i = 0; i <= Samples; i++)
                {
                    var at = (float)i / Samples;
                    if (!sampler.TryGripAt(clip.length * at, right, out var grip))
                        return null;
                    var distance = Vector3.Distance(grip.position, chest.position + chest.rotation * spot.position);
                    if (distance < nearest)
                    {
                        nearest = distance;
                        moment = at;
                    }
                }

                return moment;
            }
        }
    }
}
