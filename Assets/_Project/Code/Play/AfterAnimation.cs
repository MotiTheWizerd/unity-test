using System;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Tells, every frame, when a character's animations have posed it (Unity animates between Update and LateUpdate),
    /// so what works on the pose itself (the upper body bent to aim, an arrow laid along the bow) does so on this frame's
    /// pose, not the last. Put on a character's look by <see cref="CharacterModel"/>; <see cref="CharacterModel.Posed"/> is it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AfterAnimation : MonoBehaviour
    {
        /// <summary>This frame's pose is in place.</summary>
        public event Action Posed;

        void LateUpdate() => Posed?.Invoke();
    }
}
