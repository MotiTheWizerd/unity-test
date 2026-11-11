using System.Collections.Generic;
using System.Linq;
using Forge.Play;
using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// A humanoid import's T-pose made true where Unity's own leaves it as the model was made. Unity straightens the
    /// arms and legs of a model that stands otherwise, but keeps each hand and foot turned as it came: an AI-made model
    /// hangs its hands palms-in or thumbs-up and splays its feet. Mecanim reads every pose relative to the T-pose, so a
    /// hand turned 60° there holds a sword 60° wrong in every clip, and two characters turned differently hold it
    /// differently. Here each hand is turned so its fingers run on along the arm and its palm faces down (the hand as
    /// <see cref="GripFrame"/> reads it), its roll from how it was made shared with the forearm so the wrist isn't wrung,
    /// and each foot so its toes point forward; fingers and toes ride along. Done to
    /// the characters and to the clips' reference rig alike (<see cref="CharacterBuilder"/>), so all read one T-pose.
    /// The turns (<see cref="HandTurn"/>, <see cref="FootTurn"/>) are pure math over positions, so testable.
    /// </summary>
    static class HumanoidTPose
    {
        /// <summary>Degrees a hand or foot may be off before it is turned: below, the import is left alone.</summary>
        public const float Tolerance = 1f;

        /// <summary>
        /// Turns the hands and feet of the humanoid import at <paramref name="modelPath"/> true and reimports it; false,
        /// with no reimport, when it isn't a humanoid import or they already are. <paramref name="turned"/> says what moved.
        /// </summary>
        public static bool Straighten(string modelPath, out string turned)
        {
            turned = null;
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (importer == null || model == null || importer.animationType != ModelImporterAnimationType.Human)
                return false;

            var description = importer.humanDescription;
            if (description.skeleton == null || description.skeleton.Length == 0)
                return false;
            var pose = new SkeletonPose(description.skeleton, model.transform);
            var human = description.human.ToDictionary(h => h.humanName, h => h.boneName);
            var moved = new List<string>();
            foreach (var right in new[] { true, false })
            {
                var side = right ? "Right" : "Left";
                if (human.TryGetValue(side + "Hand", out var hand) && human.TryGetValue(side + " Thumb Proximal", out var thumb) &&
                    (human.TryGetValue(side + " Middle Proximal", out var knuckle) || human.TryGetValue(side + " Index Proximal", out knuckle)) &&
                    pose.Has(hand) && pose.Has(knuckle) && pose.Has(thumb))
                {
                    var turn = HandTurn(pose.Position(hand), pose.Position(knuckle), pose.Position(thumb), right);
                    if (pose.Turn(hand, turn, Tolerance))
                        moved.Add($"{side.ToLowerInvariant()} hand {Quaternion.Angle(Quaternion.identity, turn):F0}°");
                    if (human.TryGetValue(side + "UpperArm", out var upper) && human.TryGetValue(side + "LowerArm", out var lower) &&
                        pose.Has(upper) && pose.Has(lower) && ShareRoll(pose, model.transform, upper, lower, hand, out var shared))
                        moved.Add($"{side.ToLowerInvariant()} forearm rolls {shared:F0}°");
                }

                if (human.TryGetValue(side + "Foot", out var foot) && human.TryGetValue(side + "Toes", out var toes) && pose.Has(foot) && pose.Has(toes))
                {
                    var turn = FootTurn(pose.Position(foot), pose.Position(toes));
                    if (pose.Turn(foot, turn, Tolerance))
                        moved.Add($"{side.ToLowerInvariant()} foot {Quaternion.Angle(Quaternion.identity, turn):F0}°");
                }
            }

            if (moved.Count == 0)
                return false;
            description.skeleton = pose.Skeleton;
            importer.humanDescription = description;
            importer.SaveAndReimport();
            turned = string.Join(", ", moved);
            return true;
        }

        /// <summary>
        /// The turn (scene space) that brings a hand, read as <see cref="GripFrame"/> reads it from <paramref name="hand"/>,
        /// a <paramref name="knuckle"/> and the <paramref name="thumb"/>, to the T-pose's: fingers on along the arm (+X
        /// for the right hand, −X for the left), palm facing down.
        /// </summary>
        public static Quaternion HandTurn(Vector3 hand, Vector3 knuckle, Vector3 thumb, bool right)
        {
            var grip = GripFrame.Hand(hand, knuckle, thumb, right);
            var has = Quaternion.LookRotation(grip.rotation * Vector3.forward, (knuckle - hand).normalized);
            var wants = Quaternion.LookRotation(Vector3.down, right ? Vector3.right : Vector3.left);
            return wants * Quaternion.Inverse(has);
        }

        // The hand's roll against the model as it was made (how it was skinned), shared between the wrist and the elbow
        // as an arm rolls (HandAim.ForearmShare): truing turns the hand alone, and a mesh without twist bones shows a roll
        // where it is put, so a hand turned 75° from thumbs-up to palm-down wrings its wrist 75° in every clip. The
        // forearm takes its share about its length and the hand turns back by as much, so the hand ends where it was
        // trued. From the made model each time, so a second run moves nothing. True when the forearm turned.
        static bool ShareRoll(SkeletonPose pose, Transform model, string upper, string lower, string hand, out float shared)
        {
            shared = 0f;
            var made = model.GetComponentsInChildren<Transform>(true).ToLookup(t => t.name);
            var madeUpper = made[upper].FirstOrDefault();
            var madeLower = made[lower].FirstOrDefault();
            var madeHand = made[hand].FirstOrDefault();
            if (madeUpper == null || madeLower == null || madeHand == null)
                return false;

            var along = pose.Position(hand) - pose.Position(lower);
            var elbow = Roll(Quaternion.Inverse(pose.Rotation(upper)) * pose.Rotation(lower),
                Quaternion.Inverse(madeUpper.rotation) * madeLower.rotation, Quaternion.Inverse(pose.Rotation(upper)) * along);
            var wrist = Roll(Quaternion.Inverse(pose.Rotation(lower)) * pose.Rotation(hand),
                Quaternion.Inverse(madeLower.rotation) * madeHand.rotation, Quaternion.Inverse(pose.Rotation(lower)) * along);
            shared = Mathf.DeltaAngle(0f, HandAim.ForearmShare * (elbow + wrist) - elbow);
            var roll = Quaternion.AngleAxis(shared, along);
            if (!pose.Turn(lower, roll, Tolerance))
                return false;
            pose.Turn(hand, Quaternion.Inverse(roll), 0f);
            return true;
        }

        /// <summary>
        /// Degrees <paramref name="now"/> rolls past <paramref name="made"/> (a bone's turn against its parent, now and as
        /// made) about <paramref name="axis"/> (in the parent's frame), signed by the axis: −180 to 180.
        /// </summary>
        public static float Roll(Quaternion now, Quaternion made, Vector3 axis)
        {
            var twist = HandAim.TwistAbout(now * Quaternion.Inverse(made), axis);
            var along = Vector3.Dot(new Vector3(twist.x, twist.y, twist.z), axis.normalized);
            return Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(along, twist.w) * Mathf.Rad2Deg);
        }

        /// <summary>The turn about the up axis that points the toes of a foot at <paramref name="foot"/>, toes at <paramref name="toes"/>, forward.</summary>
        public static Quaternion FootTurn(Vector3 foot, Vector3 toes)
        {
            var ahead = toes - foot;
            ahead.y = 0f;
            return ahead.sqrMagnitude > 0f ? Quaternion.FromToRotation(ahead.normalized, Vector3.forward) : Quaternion.identity;
        }

        // The description's skeleton as a pose in scene space, each bone's parent read off the model's own hierarchy.
        sealed class SkeletonPose
        {
            readonly SkeletonBone[] _bones;
            readonly Dictionary<string, int> _index = new Dictionary<string, int>();
            readonly Dictionary<string, string> _parents = new Dictionary<string, string>();

            public SkeletonPose(SkeletonBone[] skeleton, Transform root)
            {
                _bones = (SkeletonBone[])skeleton.Clone();
                for (var i = 0; i < _bones.Length; i++)
                    _index[_bones[i].name] = i;
                // The description names the model's root after the instance it was read from ("VikingHero(Clone)"), first
                // in the list, while a mesh under it may bear the model's own name ("VikingHero"); so the root transform is
                // the first bone, every other transform is the bone of its name, and a bone's parent is its nearest
                // ancestor that is a bone.
                var rootBone = _bones[0].name;
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform == root || !_index.ContainsKey(transform.name) || _parents.ContainsKey(transform.name))
                        continue;
                    var parent = transform.parent;
                    while (parent != null && parent != root && !_index.ContainsKey(parent.name))
                        parent = parent.parent;
                    _parents[transform.name] = parent == null ? null : parent == root ? rootBone : parent.name;
                }
            }

            public SkeletonBone[] Skeleton => _bones;

            public bool Has(string bone) => _index.ContainsKey(bone);

            public Vector3 Position(string bone)
            {
                var parent = ParentOf(bone);
                var local = _bones[_index[bone]].position;
                return parent == null ? local : Position(parent) + Rotation(parent) * local;
            }

            public Quaternion Rotation(string bone)
            {
                var parent = ParentOf(bone);
                var local = _bones[_index[bone]].rotation;
                return parent == null ? local : Rotation(parent) * local;
            }

            /// <summary>Turns <paramref name="bone"/> by <paramref name="turn"/> (scene space), children riding along; false when the turn is under <paramref name="tolerance"/> degrees.</summary>
            public bool Turn(string bone, Quaternion turn, float tolerance)
            {
                if (Quaternion.Angle(Quaternion.identity, turn) < tolerance)
                    return false;
                var parent = ParentOf(bone);
                var world = turn * Rotation(bone);
                var at = _index[bone];
                _bones[at].rotation = parent == null ? world : Quaternion.Inverse(Rotation(parent)) * world;
                return true;
            }

            string ParentOf(string bone) =>
                _parents.TryGetValue(bone, out var parent) && parent != null && parent != bone && _index.ContainsKey(parent) ? parent : null;
        }
    }
}
