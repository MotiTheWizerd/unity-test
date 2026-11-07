using UnityEngine;

namespace Forge.Play
{
    /// <summary>A humanoid's hands as its Animator maps them: where each grips (<see cref="GripFrame"/>), as it stands now.</summary>
    public static class HumanoidHands
    {
        /// <summary>The hand bone of the right or the left hand.</summary>
        public static HumanBodyBones Hand(bool right) => right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;

        /// <summary>
        /// Where the right or left hand of <paramref name="animator"/> grips now (scene space), from the hand, a knuckle (the
        /// middle finger's, else the index's) and the thumb; false when its rig lacks them.
        /// </summary>
        public static bool TryGrip(Animator animator, bool right, out Pose grip)
        {
            grip = default;
            var hand = animator.GetBoneTransform(Hand(right));
            var knuckle = animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            if (knuckle == null)
                knuckle = animator.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var thumb = animator.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            if (hand == null || knuckle == null || thumb == null)
                return false;
            grip = GripFrame.Hand(hand.position, knuckle.position, thumb.position, right);
            return true;
        }
    }
}
