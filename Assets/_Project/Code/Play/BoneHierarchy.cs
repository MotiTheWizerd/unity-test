using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>Where things hang in a character's skeleton.</summary>
    static class BoneHierarchy
    {
        /// <summary>Whether <paramref name="transform"/> is one of <paramref name="bones"/> or hangs, however deep, from one.</summary>
        public static bool HangsFrom(this Transform transform, ICollection<Transform> bones)
        {
            for (var at = transform; at != null; at = at.parent)
            {
                if (bones.Contains(at))
                    return true;
            }

            return false;
        }
    }
}
