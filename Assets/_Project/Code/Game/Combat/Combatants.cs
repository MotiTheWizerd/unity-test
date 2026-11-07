using System;
using System.Collections.Generic;
using Forge.Game.Actors;
using Forge.Game.Attributes;

namespace Forge.Game.Combat
{
    /// <summary>
    /// Who can be struck in a game, by actor: each with the attributes a blow acts on. The player is one while spawned;
    /// the world's actors join as they come into play (a training dummy, later its characters). Whoever runs the game owns
    /// one and empties it when the game ends. Engine-free.
    /// </summary>
    public sealed class Combatants
    {
        readonly Dictionary<ActorId, ActorAttributes> _all = new Dictionary<ActorId, ActorAttributes>();
        readonly Dictionary<ActorId, string> _names = new Dictionary<ActorId, string>();

        /// <summary>
        /// Makes <paramref name="actor"/> one who can be struck, through <paramref name="attributes"/>, known to a player
        /// as <paramref name="name"/> ("Training Dummy"); replaces whoever it was.
        /// </summary>
        public void Add(ActorId actor, ActorAttributes attributes, string name = null)
        {
            if (actor.IsDefault)
                throw new ArgumentException("A combatant is an actor.", nameof(actor));
            _all[actor] = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _names[actor] = string.IsNullOrWhiteSpace(name) ? actor.Key : name;
        }

        public bool Remove(ActorId actor)
        {
            _names.Remove(actor);
            return _all.Remove(actor);
        }

        /// <summary>What a player reads for <paramref name="actor"/> (its name, else its key); null when it is no combatant.</summary>
        public string NameOf(ActorId actor) => _names.TryGetValue(actor, out var name) ? name : null;

        public bool TryGet(ActorId actor, out ActorAttributes attributes) => _all.TryGetValue(actor, out attributes);

        public bool Contains(ActorId actor) => _all.ContainsKey(actor);

        public void Clear()
        {
            _all.Clear();
            _names.Clear();
        }
    }
}
