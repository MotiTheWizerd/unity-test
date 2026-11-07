using System;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Events;
using Forge.Game.Interaction;
using Forge.Game.Items;
using Forge.Game.World;
using Forge.World;
using UnityEngine;
// Not UnityEngine.Compass, the device's magnetometer.
using Compass = Forge.Core.Geometry.Compass;

namespace Forge.Play
{
    /// <summary>
    /// One running game beside the world it plays: its stream (<see cref="Events"/>), what playing changed in the world
    /// (<see cref="Changes"/>, shown over the scene: what was taken leaves it, what was dropped joins it), the world as
    /// played (<see cref="World"/>), what actors can do with the use key (<see cref="Interactions"/>: take items) and the
    /// player (<see cref="Player"/>). <see cref="Begin"/> stands the player in the world, <see cref="Tick"/> plays a frame
    /// and then tells what happened in it, <see cref="End"/> takes the player away and puts the world back as built; the
    /// player keeps what it carries, as it keeps its attributes. Whatever plays the world runs one: the editor's play from
    /// here today, the game itself later.
    /// </summary>
    public sealed class GameSession : IDisposable
    {
        // How far ahead of the player's feet a dropped item is set down, in meters.
        const float DropReach = 0.9f;

        readonly WorldView _view;
        readonly WorldFrame _frame;
        readonly IItemPlacement _placement;
        readonly ItemDropper _dropper;

        /// <param name="camera">The camera the player sees through while playing.</param>
        /// <param name="view">The scene's world, which shows what playing changed.</param>
        /// <param name="frame">The world's frame in the scene, where the player's positions become the world's.</param>
        /// <param name="placement">Where a dropped item comes to rest.</param>
        /// <param name="onError">Where a listener that fails is reported; the others still hear.</param>
        public GameSession(PlayerProfile profile, Transform camera, WorldView view, WorldFrame frame, IItemPlacement placement, Action<Exception> onError)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _placement = placement ?? throw new ArgumentNullException(nameof(placement));
            Events = new GameEvents(onError);
            Changes = new WorldChanges();
            Player = new PlayerSession(profile, camera, Events);
            Interactions = new Interactions()
                .Add(new TakeInteraction(Changes, InventoryOf, Events));
            _dropper = new ItemDropper(Changes, InventoryOf, Events);
            Changes.Added += _view.ShowAdded;
            Changes.Removed += OnRemoved;
            Changes.Cleared += _view.ShowAsBuilt;
        }

        /// <summary>The game's stream; this session flushes it once a frame.</summary>
        public GameEvents Events { get; }

        /// <summary>What playing changed in the world, let go when playing ends.</summary>
        public WorldChanges Changes { get; }

        /// <summary>The world as played (as built, with <see cref="Changes"/> on top) while playing; else null.</summary>
        public IRecordStore World { get; private set; }

        /// <summary>What actors can do with the use key, in order.</summary>
        public Interactions Interactions { get; }

        public PlayerSession Player { get; }

        /// <summary>
        /// Starts playing <paramref name="world"/>: the player stands at <paramref name="feet"/> (scene coordinates), facing
        /// <paramref name="heading"/>.
        /// </summary>
        public void Begin(Vector3 feet, float heading, IRecordStore world)
        {
            World = new PlayedWorld(world ?? throw new ArgumentNullException(nameof(world)), Changes);
            Player.Spawn(feet, heading, World, Interactions);
            Events.Flush();
        }

        /// <summary>Plays one frame, then tells what happened in it.</summary>
        public void Tick(float deltaTime)
        {
            Player.Tick(deltaTime);
            Events.Flush();
        }

        /// <summary>
        /// The back key (Esc): closes what the player has open (the inventory); false when nothing was, and whatever hosts
        /// the game decides what back means then (the editor goes back to editing; the game will open its menu).
        /// </summary>
        public bool Back()
        {
            if (!Player.InventoryOpen)
                return false;
            Player.CloseInventory();
            return true;
        }

        /// <summary>
        /// The player drops one <paramref name="item"/> it carries: it comes to rest on whatever is a little ahead of its
        /// feet (else at them), turned the way the player faces. The placed object it now is, or null when nothing was dropped.
        /// </summary>
        public PlacedObjectRecord Drop(RecordId item)
        {
            var player = Player.Player;
            if (player == null || !ItemRecords.TryGet(World, item, out var record))
                return null;

            var heading = player.BodyHeading;
            var ahead = Compass.Forward(heading) * DropReach;
            var above = _frame.ToWorld((player.Feet + new System.Numerics.Vector3(ahead.X, player.EyeHeight, ahead.Y)).ToUnity()).ToNumerics();
            if (!_placement.TryRest(record, above, heading, out var position, out var rotation))
            {
                position = _frame.ToWorld(player.Feet.ToUnity()).ToNumerics();
                rotation = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, heading * Compass.DegreesToRadians);
            }

            var placed = _dropper.Drop(ActorId.Player, item, _view.Space, position, rotation);
            Events.Flush();
            return placed;
        }

        /// <summary>Stops playing: the player goes, and the world is as built again.</summary>
        public void End()
        {
            Player.Despawn();
            Changes.Clear();
            World = null;
            Events.Flush();
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); the player moves with it.</summary>
        public void Shift(Vector3 delta) => Player.Shift(delta);

        public void Dispose()
        {
            Changes.Added -= _view.ShowAdded;
            Changes.Removed -= OnRemoved;
            Changes.Cleared -= _view.ShowAsBuilt;
            Player.Dispose();
        }

        Inventory InventoryOf(ActorId actor) => actor == ActorId.Player ? Player.Inventory : null;

        // What was taken leaves the scene, and the player looks again at what is in sight.
        void OnRemoved(RecordId placed)
        {
            _view.Hide(placed);
            Player.Focus?.Refresh();
        }
    }
}
