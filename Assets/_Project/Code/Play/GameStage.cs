using System;
using Forge.Data.Plugins;
using Forge.Game.Actors;
using Forge.Game.Combat;
using Forge.Game.Events;
using Forge.Play.Sound;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// What one game gives every actor standing in it (<see cref="PlayedActor"/>), the player and the world's characters
    /// alike: the world as played (<see cref="World"/>, which its items, styles and slots are read from), the stream
    /// (<see cref="Events"/>), the looks of the world's things (<see cref="Assets"/>, for what is held), who can be
    /// struck (<see cref="Combatants"/>, the world's placed actors among them as they are met, <see cref="PlacedActors"/>),
    /// where a shot that lands goes (<see cref="LandShot"/>), what sounds play on (<see cref="Sounds"/>; null: the game
    /// plays silent) and what is underfoot (<see cref="Surfaces"/>; null: nothing in particular). One per game begun
    /// (<see cref="GameSession.Begin"/>), gone when it ends.
    /// </summary>
    public sealed class GameStage
    {
        public GameStage(IRecordStore world, GameEvents events, IAssetCatalog assets, Combatants combatants, PlacedActors placedActors,
            ArrowFlights.Landing landShot, SoundPlayer sounds = null, GroundSurfaces surfaces = null)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Assets = assets ?? throw new ArgumentNullException(nameof(assets));
            Combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            PlacedActors = placedActors ?? throw new ArgumentNullException(nameof(placedActors));
            LandShot = landShot ?? throw new ArgumentNullException(nameof(landShot));
            Sounds = sounds;
            Surfaces = surfaces;
        }

        /// <summary>The world as played, which every actor's items, styles and slots are read from.</summary>
        public IRecordStore World { get; }

        /// <summary>The game's stream, which every actor's happenings go on.</summary>
        public GameEvents Events { get; }

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

        /// <summary>
        /// Whom a collider in the scene belongs to: a placed actor of the world, by its placed object
        /// (<see cref="RecordLink"/>); null for no one who can be struck.
        /// </summary>
        public ActorId? Identify(Collider collider)
        {
            if (collider == null)
                return null;
            var link = collider.GetComponentInParent<RecordLink>();
            return link != null && PlacedActors.TryGet(World, link.RecordId, out var actor) ? actor : (ActorId?)null;
        }
    }
}
