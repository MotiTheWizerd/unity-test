using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Where a hand holds a thing's look. A model asset (Content/Models) is a root holding the model, whose own pivot is
    /// its grip; anything else is held by its root. How the thing is held and carried beyond that is its
    /// <see cref="HeldLook"/>, if it has one.
    /// </summary>
    public static class ModelGrip
    {
        /// <summary>The grip <paramref name="look"/> is held by.</summary>
        public static Transform Of(GameObject look) =>
            look.transform.childCount == 1 ? look.transform.GetChild(0) : look.transform;
    }
}
