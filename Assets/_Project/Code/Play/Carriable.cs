using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A loose body that is no world object but may be taken up and carried all the same (<see cref="ObjectCarry"/>): a part
    /// of a body gone limp (<see cref="Ragdoll"/>). It says what it moves with when the scene moves under the world
    /// (<see cref="Frame"/>: a carrier's grip hangs there too, so a rebase moves both alike), and every body it is joined
    /// to (<see cref="Whole"/>), which go together where one is thrown. Runtime-only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Carriable : MonoBehaviour
    {
        /// <summary>What it moves with when the scene moves under the world.</summary>
        public Transform Frame { get; internal set; }

        /// <summary>Every body it is joined to, itself among them.</summary>
        public IReadOnlyList<Rigidbody> Whole { get; internal set; }
    }
}
