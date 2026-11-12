using System;
using System.Linq;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Combat;
using Forge.Game.Events;
using Forge.Game.Minds;
using Forge.Play.Sound;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// What one game gives every actor standing in it (<see cref="PlayedActor"/>), the player and the world's characters
    /// alike: the world as played (<see cref="World"/>, which its items, styles and slots are read from) and its
    /// attributes (<see cref="Attributes"/>), the stream (<see cref="Events"/>), what every actor is up to
    /// (<see cref="Modes"/>), the looks of the world's things
    /// (<see cref="Assets"/>, for what is held), who can be struck (<see cref="Combatants"/>, the world's placed actors
    /// among them as they are met, <see cref="PlacedActors"/>), who stands in the game (<see cref="Actors"/>), where a
    /// shot that lands goes (<see cref="LandShot"/>), what sounds play on (<see cref="Sounds"/>; null: the game plays
    /// silent), what is underfoot (<see cref="Surfaces"/>; null: nothing in particular) and who sees whom
    /// (<see cref="Sight"/>). One per game begun (<see cref="GameSession.Begin"/>), gone when it ends.
    /// </summary>
    public sealed class GameStage
    {
        public GameStage(IRecordStore world, GameEvents events, ActorModes modes, IAssetCatalog assets, Combatants combatants,
            PlacedActors placedActors, ArrowFlights.Landing landShot, SoundPlayer sounds = null, GroundSurfaces surfaces = null)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Modes = modes ?? throw new ArgumentNullException(nameof(modes));
            Assets = assets ?? throw new ArgumentNullException(nameof(assets));
            Combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            PlacedActors = placedActors ?? throw new ArgumentNullException(nameof(placedActors));
            LandShot = landShot ?? throw new ArgumentNullException(nameof(landShot));
            Sounds = sounds;
            Surfaces = surfaces;
            Actors = new ActorRoster();
            // A world made before attributes (none in it) plays as before: nothing costs, nothing is hurt.
            var attributes = world.OfType<AttributeRecord>().ToArray();
            Attributes = attributes.Length > 0 ? AttributeSet.From(attributes) : null;
        }

        /// <summary>The world as played, which every actor's items, styles and slots are read from.</summary>
        public IRecordStore World { get; }

        /// <summary>The world's attributes (health, stamina, ...), resolved once for every actor of the game; null in a world made before attributes.</summary>
        public AttributeSet Attributes { get; }

        /// <summary>Every actor standing in the game, by who it is.</summary>
        public ActorRoster Actors { get; }

        /// <summary>The game's stream, which every actor's happenings go on.</summary>
        public GameEvents Events { get; }

        /// <summary>What every actor is up to (at ease, fighting whom), which a character's mind reads.</summary>
        public ActorModes Modes { get; }

        /// <summary>The looks of the world's things, for what an actor holds and shoots.</summary>
        public IAssetCatalog Assets { get; }

        /// <summary>Who can be struck in the game; an actor with attributes is one while it stands.</summary>
        public Combatants Combatants { get; }

        /// <summary>The world's placed actors (a training dummy), which blows find by their placed objects.</summary>
        public PlacedActors PlacedActors { get; }

        /// <summary>Where what an actor shoots goes once it lands, with its pose there and what it is in.</summary>
        public ArrowFlights.Landing LandShot { get; }

        /// <summary>What an actor's sounds (its footsteps) play on; null: the game plays silent.</summary>
        public SoundPlayer Sounds { get; }

        /// <summary>What is underfoot, which steps sound of; null: nothing in particular.</summary>
        public GroundSurfaces Surfaces { get; }

        /// <summary>Whether one point sees another past the world's solid things, which characters' eyes go by.</summary>
        public ILineOfSight Sight { get; } = new PhysicsLineOfSight();

        /// <summary>
        /// Whom a collider in the scene belongs to: an actor's body (<see cref="ActorLink"/>), or a placed actor of the
        /// world by its placed object (<see cref="RecordLink"/>); null for no one who can be struck.
        /// </summary>
        public ActorId? Identify(Collider collider)
        {
            if (collider == null)
                return null;
            var body = collider.GetComponentInParent<ActorLink>();
            if (body != null && !body.Actor.IsDefault)
                return body.Actor;
            var link = collider.GetComponentInParent<RecordLink>();
            return link != null && PlacedActors.TryGet(World, link.RecordId, out var actor) ? actor : (ActorId?)null;
        }
    }
}
