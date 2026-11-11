using Forge.Game.Actors;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Tags an actor's body (<see cref="ActorBody"/>) with who it is, so a blow or an arrow that meets the body finds
    /// the actor (<see cref="GameStage.Identify"/>), as <see cref="Forge.World.RecordLink"/> finds a placed object from
    /// its scene object. Runtime-only state, set by the body.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorLink : MonoBehaviour
    {
        public ActorId Actor { get; internal set; }
    }
}
