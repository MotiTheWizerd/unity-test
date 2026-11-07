using System;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Game.Actors;
using Forge.Game.Events;
using Forge.Game.Interaction;
using Forge.Game.Items;
using Forge.Game.World;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// One running game beside the world it plays: its stream (<see cref="Events"/>), what playing changed in the world
    /// (<see cref="Changes"/>, shown by taking what was taken out of the scene), what actors can do with the use key
    /// (<see cref="Interactions"/>: take items) and the player (<see cref="Player"/>). <see cref="Begin"/> stands the
    /// player in the world, <see cref="Tick"/> plays a frame and then tells what happened in it, <see cref="End"/> takes
    /// the player away and puts the world back as built; the player keeps what it carries, as it keeps its attributes.
    /// Whatever plays the world runs one: the editor's play from here today, the game itself later.
    /// </summary>
    public sealed class GameSession : IDisposable
    {
        readonly WorldView _view;

        /// <param name="camera">The camera the player sees through while playing.</param>
        /// <param name="view">The scene's world, which shows what playing changed.</param>
        /// <param name="onError">Where a listener that fails is reported; the others still hear.</param>
        public GameSession(PlayerProfile profile, Transform camera, WorldView view, Action<Exception> onError)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            Events = new GameEvents(onError);
            Changes = new WorldChanges();
            Player = new PlayerSession(profile, camera, Events);
            Interactions = new Interactions()
                .Add(new TakeInteraction(Changes, actor => actor == ActorId.Player ? Player.Inventory : null, Events));
            Changes.Removed += OnRemoved;
            Changes.Cleared += _view.ShowHidden;
        }

        /// <summary>The game's stream; this session flushes it once a frame.</summary>
        public GameEvents Events { get; }

        /// <summary>What playing changed in the world, let go when playing ends.</summary>
        public WorldChanges Changes { get; }

        /// <summary>What actors can do with the use key, in order.</summary>
        public Interactions Interactions { get; }

        public PlayerSession Player { get; }

        /// <summary>
        /// Starts playing <paramref name="world"/>: the player stands at <paramref name="feet"/> (scene coordinates), facing
        /// <paramref name="heading"/>.
        /// </summary>
        public void Begin(Vector3 feet, float heading, IRecordStore world)
        {
            Player.Spawn(feet, heading, world, Interactions);
            Events.Flush();
        }

        /// <summary>Plays one frame, then tells what happened in it.</summary>
        public void Tick(float deltaTime)
        {
            Player.Tick(deltaTime);
            Events.Flush();
        }

        /// <summary>Stops playing: the player goes, and the world is as built again.</summary>
        public void End()
        {
            Player.Despawn();
            Changes.Clear();
            Events.Flush();
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); the player moves with it.</summary>
        public void Shift(Vector3 delta) => Player.Shift(delta);

        public void Dispose()
        {
            Changes.Removed -= OnRemoved;
            Changes.Cleared -= _view.ShowHidden;
            Player.Dispose();
        }

        // What was taken leaves the scene, and the player looks again at what is in sight.
        void OnRemoved(RecordId placed)
        {
            _view.Hide(placed);
            Player.Focus?.Refresh();
        }
    }
}
