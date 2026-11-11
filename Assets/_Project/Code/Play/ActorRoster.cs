using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Game.Actors;

namespace Forge.Play
{
    /// <summary>
    /// Every actor standing in a game (<see cref="PlayedActor"/>), by who it is: the player while spawned, the world's
    /// characters while they go about. Each joins as it is made and leaves as it is disposed; whoever has only an
    /// actor's id and needs the actor itself (where it is, what it carries, how it looks) asks here.
    /// </summary>
    public sealed class ActorRoster : IStandingActors
    {
        readonly Dictionary<ActorId, PlayedActor> _standing = new Dictionary<ActorId, PlayedActor>();

        /// <summary>How many actors stand in the game.</summary>
        public int Count => _standing.Count;

        /// <summary>Every actor standing, in no order.</summary>
        public IEnumerable<PlayedActor> All => _standing.Values;

        /// <summary>The actor <paramref name="actor"/> is, while it stands; false when it doesn't.</summary>
        public bool TryGet(ActorId actor, out PlayedActor standing) => _standing.TryGetValue(actor, out standing);

        /// <summary>Where <paramref name="actor"/>'s feet are (the scene's coordinates) while it stands up; null when it isn't in the game or lies fallen.</summary>
        public Vector3? FeetOf(ActorId actor) =>
            _standing.TryGetValue(actor, out var standing) && !standing.Fallen ? standing.Rules.Feet : (Vector3?)null;

        internal void Add(PlayedActor actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            _standing[actor.Id] = actor;
        }

        // Only the actor itself leaves, not another that took its id meanwhile.
        internal void Remove(PlayedActor actor)
        {
            if (actor != null && _standing.TryGetValue(actor.Id, out var standing) && ReferenceEquals(standing, actor))
                _standing.Remove(actor.Id);
        }
    }
}
