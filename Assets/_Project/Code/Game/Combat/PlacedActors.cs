using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Interaction;

namespace Forge.Game.Combat
{
    /// <summary>
    /// The world's placed actors as combatants: a placed object whose base object names an actor
    /// (<see cref="BaseObjectRecord.Actor"/>: a training dummy, later a character) joins <see cref="Combatants"/> the first
    /// time something asks for it, with the world's attributes at their base values, and stays (hurt as it was) until
    /// the game ends (<see cref="Clear"/>). Each is known by its placed object (<see cref="IdOf"/>). Engine-free.
    /// </summary>
    public sealed class PlacedActors
    {
        /// <summary>The prefix of a placed actor's id: "placed:" and its placed object's id.</summary>
        public const string Prefix = "placed:";

        readonly Combatants _combatants;
        IRecordStore _world;
        AttributeSet _attributes;

        public PlacedActors(Combatants combatants) => _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));

        /// <summary>The actor the placed object <paramref name="placed"/> is.</summary>
        public static ActorId IdOf(RecordId placed) => new ActorId(Prefix + placed);

        /// <summary>
        /// The actor <paramref name="placed"/> is in <paramref name="world"/>, now one who can be struck; false when it is
        /// no actor (just a thing, or its actor is missing from the world).
        /// </summary>
        public bool TryGet(IRecordStore world, RecordId placed, out ActorId actor)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            actor = default;
            if (!InteractionTarget.TryResolve(world, placed, out var target) || !target.Kind.Actor.HasValue ||
                !world.TryGet(target.Kind.Actor.Value, out var record) || !(record is ActorRecord actorRecord))
                return false;

            actor = IdOf(placed);
            if (!_combatants.Contains(actor))
                _combatants.Add(actor, new ActorAttributes(AttributesOf(world), actor), actorRecord.DisplayName);
            return true;
        }

        /// <summary>Forgets every placed actor (the game ended; the world is as built again).</summary>
        public void Clear()
        {
            _world = null;
            _attributes = null;
        }

        // The world's attribute set, read once per world.
        AttributeSet AttributesOf(IRecordStore world)
        {
            if (!ReferenceEquals(world, _world))
            {
                _world = world;
                _attributes = AttributeSet.From(world.OfType<AttributeRecord>());
            }

            return _attributes;
        }
    }
}
