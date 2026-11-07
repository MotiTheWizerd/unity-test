using System;
using System.Collections.Generic;
using Forge.Game.Actors;

namespace Forge.Game.Interaction
{
    /// <summary>
    /// The things actors can do to placed objects, in order: the first to make an offer for a target is what the use key
    /// does with it (one verb per thing, as in Skyrim). A new kind of thing (a door, a container) is one more interaction
    /// added here, reading its own part; nothing else changes. One per running game.
    /// </summary>
    public sealed class Interactions
    {
        readonly List<IInteraction> _all = new List<IInteraction>();

        public Interactions Add(IInteraction interaction)
        {
            _all.Add(interaction ?? throw new ArgumentNullException(nameof(interaction)));
            return this;
        }

        /// <summary>The first offer an interaction makes for <paramref name="target"/>, and the one that made it.</summary>
        public bool TryOffer(InteractionTarget target, ActorId actor, out IInteraction interaction, out InteractionOffer offer)
        {
            foreach (var candidate in _all)
            {
                offer = candidate.Offer(target, actor);
                if (offer != null)
                {
                    interaction = candidate;
                    return true;
                }
            }

            interaction = null;
            offer = null;
            return false;
        }
    }
}
