using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Where a hand holds a thing's look. A model asset (Content/Models) is a root holding the model, whose own pivot is
    /// its grip; in the left hand it is held by its left-hand grip when it has one (<see cref="LeftHand"/>, a turn the
    /// model build makes from its {Name}.json "leftGrip": a bow turned half round its length, so its string faces the
    /// archer in either hand). Anything else is held by its root.
    /// </summary>
    public static class ModelGrip
    {
        /// <summary>The name of a model's left-hand grip, a child of the model at its pivot.</summary>
        public const string LeftHand = "Grip (left hand)";

        /// <summary>The grip <paramref name="look"/> is held by, in the left hand or the right.</summary>
        public static Transform Of(GameObject look, bool left)
        {
            var model = look.transform.childCount == 1 ? look.transform.GetChild(0) : look.transform;
            var leftGrip = left ? model.Find(LeftHand) : null;
            return leftGrip != null ? leftGrip : model;
        }
    }
}
