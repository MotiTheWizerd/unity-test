using System;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Combat;
using Forge.Game.Equipment;
using Forge.Game.Events;
using Forge.Game.Interaction;
using Forge.Game.Items;
using Forge.Game.World;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;
// Not UnityEngine.Compass, the device's magnetometer.
using Compass = Forge.Core.Geometry.Compass;

namespace Forge.Play
{
    /// <summary>
    /// One running game beside the world it plays: its stream (<see cref="Events"/>), what playing changed in the world
    /// (<see cref="Changes"/>, shown over the scene: what was taken leaves it, what was dropped joins it), the world as
    /// played (<see cref="World"/>), what actors can do with the use key (<see cref="Interactions"/>: take items) and the
    /// player (<see cref="Player"/>). <see cref="Begin"/> gives the player its starting items and stands it in the world, <see cref="Tick"/> plays a frame
    /// and then tells what happened in it, <see cref="End"/> takes the player away and puts the world back as built, with
    /// what the player took from it, and the hurt it did forgotten. Whatever plays the world runs one: the editor's play from
    /// here today, the game itself later.
    /// </summary>
    public sealed class GameSession : IDisposable
    {
        // Where a dropped item is let go, meters: this far ahead of the eyes the way the body faces, this far below them
        // (the hands, before the chest), and this far clear of what stands nearer (a wall).
        const float DropReach = 0.6f;
        const float DropBelowEyes = 0.5f;
        const float DropClearance = 0.1f;

        // How a dropped item leaves the hands: pushed this fast ahead of the body, meters a second, and turning at most
        // this fast, radians a second, so it tumbles as it lands rather than falling flat like a placed one.
        const float DropToss = 0.5f;
        const float DropSpin = 3f;

        readonly WorldView _view;
        readonly WorldFrame _frame;
        readonly ItemDropper _dropper;
        readonly ShotLandings _landings;

        /// <param name="camera">The camera the player sees through while playing.</param>
        /// <param name="view">The scene's world, which shows what playing changed.</param>
        /// <param name="frame">The world's frame in the scene, where the player's positions become the world's.</param>
        /// <param name="onError">Where a listener that fails is reported; the others still hear.</param>
        public GameSession(PlayerProfile profile, Transform camera, WorldView view, WorldFrame frame, Action<Exception> onError)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Events = new GameEvents(onError);
            Changes = new WorldChanges();
            Combatants = new Combatants();
            PlacedActors = new PlacedActors(Combatants);
            Player = new PlayerSession(profile, camera, Events, view.Assets, Combatants, PlacedActors, LandShot);
            Interactions = new Interactions()
                .Add(new TakeInteraction(Changes, InventoryOf, Events));
            _dropper = new ItemDropper(Changes, InventoryOf, Events);
            _landings = new ShotLandings(Changes, Events);
            Changes.Added += _view.ShowAdded;
            Changes.Moved += _view.ShowMoved;
            Changes.Removed += OnRemoved;
            Changes.Cleared += _view.ShowAsBuilt;
        }

        /// <summary>The game's stream; this session flushes it once a frame.</summary>
        public GameEvents Events { get; }

        /// <summary>What playing changed in the world, let go when playing ends.</summary>
        public WorldChanges Changes { get; }

        /// <summary>The world as played (as built, with <see cref="Changes"/> on top) while playing; else null.</summary>
        public IRecordStore World { get; private set; }

        /// <summary>The looks of the world's things, by asset key, for whatever shows them apart from the world (the inventory's preview).</summary>
        public IAssetCatalog Assets => _view.Assets;

        /// <summary>Who can be struck in this game: the player while spawned, the world's actors once met; emptied when it ends.</summary>
        public Combatants Combatants { get; }

        /// <summary>The world's placed actors (a training dummy) as combatants, by their placed objects.</summary>
        public PlacedActors PlacedActors { get; }

        /// <summary>What actors can do with the use key, in order.</summary>
        public Interactions Interactions { get; }

        public PlayerSession Player { get; }

        /// <summary>
        /// Starts a game of <paramref name="world"/> (ending one already running): the player starts with what its record
        /// gives it (<see cref="CoreActors.Player"/>: carried, some of it equipped, weapons sheathed) and stands at
        /// <paramref name="feet"/> (scene coordinates), facing <paramref name="heading"/>.
        /// </summary>
        public void Begin(Vector3 feet, float heading, IRecordStore world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            if (World != null)
                End();

            World = new PlayedWorld(world, Changes);
            Bodies = new WorldPhysics(_view, World, OnSettled);
            if (World.TryGet(CoreActors.Player, out var record) && record is ActorRecord player)
                StartingItems.Give(player, World, Player.Inventory, Player.Equipment);
            Player.Spawn(feet, heading, World, Interactions);
            Events.Flush();
        }

        /// <summary>The world's loose things as physical bodies while playing; else null.</summary>
        public WorldPhysics Bodies { get; private set; }

        /// <summary>Plays one frame, then tells what happened in it.</summary>
        public void Tick(float deltaTime)
        {
            Player.Tick(deltaTime);
            Bodies?.Tick();
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
        /// The player drops one <paramref name="item"/> it carries, as Skyrim's player does: let go from its hands, a
        /// little ahead of its chest the way its body faces (nearer when a wall is), turned that way, it falls and tumbles,
        /// and stays wherever it comes to rest (<see cref="WorldPhysics"/>). The placed object it now is, or null when
        /// nothing was dropped.
        /// </summary>
        public PlacedObjectRecord Drop(RecordId item)
        {
            var player = Player.Player;
            if (player == null || Bodies == null || !ItemRecords.TryGet(World, item, out _))
                return null;

            var heading = player.BodyHeading;
            var ahead = Compass.Forward(heading);
            var way = new Vector3(ahead.X, 0f, ahead.Y);
            var hands = player.Eyes.ToUnity() + Vector3.down * DropBelowEyes;
            var reach = Physics.SphereCast(hands, DropClearance, way, out var wall, DropReach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? wall.distance
                : DropReach;
            var position = _frame.ToWorld(hands + way * reach).ToNumerics();
            var rotation = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, heading * Compass.DegreesToRadians);

            var placed = _dropper.Drop(ActorId.Player, item, _view.Space, position, rotation);
            if (placed != null)
            {
                // Shown at once (the player's own chunk is loaded), so its body is there to let go. It goes on as the player
                // walks; not as it falls, since standing, the body is held to the ground by a fall of its own.
                var moving = player.Locomotion.Velocity;
                var velocity = new Vector3(moving.X, 0f, moving.Z) + way * DropToss;
                if (!Bodies.Release(placed.Id, velocity, UnityEngine.Random.insideUnitSphere * DropSpin))
                    Debug.LogWarning($"[Forge] '{item}' has no collider, so it can't fall: it stays where it was let go.");
            }

            Events.Flush();
            return placed;
        }

        /// <summary>
        /// The player equips <paramref name="item"/>, from what it carries: in <paramref name="slot"/> when given (a
        /// sword in the left hand), else wherever it fits best (<see cref="ActorEquipment.TryEquip"/>).
        /// </summary>
        public EquipOutcome Equip(RecordId item, RecordId? slot = null)
        {
            if (Player.Player == null)
                return EquipOutcome.NotCarried;
            if (!ItemRecords.TryGet(World, item, out var record))
                return EquipOutcome.NotEquippable;
            var outcome = Player.Equipment.TryEquip(record, slot);
            Events.Flush();
            return outcome;
        }

        /// <summary>The player takes off what <paramref name="slot"/> holds; false when it held nothing.</summary>
        public bool Unequip(RecordId slot)
        {
            var unequipped = Player.Equipment.Unequip(slot);
            Events.Flush();
            return unequipped;
        }

        /// <summary>
        /// Stops playing: the player goes, and the world is as built again, as the Creation Kit's test play leaves it.
        /// What the player took goes back with it, so playing again never finds an item both in the pack and on the ground;
        /// what it had equipped comes off with it.
        /// </summary>
        public void End()
        {
            Player.Despawn();
            Bodies?.Dispose();
            Bodies = null;
            Player.Inventory.Clear();
            Combatants.Clear();
            PlacedActors.Clear();
            Changes.Clear();
            World = null;
            Events.Flush();
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); the player moves with it.</summary>
        public void Shift(Vector3 delta)
        {
            Player.Shift(delta);
            Bodies?.Shift(delta);
        }

        public void Dispose()
        {
            Changes.Added -= _view.ShowAdded;
            Changes.Moved -= _view.ShowMoved;
            Changes.Removed -= OnRemoved;
            Changes.Cleared -= _view.ShowAsBuilt;
            Player.Dispose();
        }

        Inventory InventoryOf(ActorId actor) => actor == ActorId.Player ? Player.Inventory : null;

        // A loose thing came to rest somewhere new (scene space): playing keeps it there, in the world's own coordinates.
        void OnSettled(RecordId id, Vector3 position, Quaternion rotation)
        {
            if (World != null && World.TryGet(id, out var record) && record is PlacedObjectRecord placed)
                Changes.Move(placed.WithTransform(_frame.ToWorld(position).ToNumerics(), rotation.ToNumerics(), placed.Scale));
        }

        // What was shot landed at a pose in the scene: it stays there in the world's own coordinates as the item it is,
        // to be taken back, unless it broke.
        void LandShot(Shot shot, Vector3 position, Quaternion rotation)
        {
            if (World != null)
                _landings.Land(shot, _view.Space, _frame.ToWorld(position).ToNumerics(), rotation.ToNumerics());
        }

        // What was taken leaves the scene, and the player looks again at what is in sight.
        void OnRemoved(RecordId placed)
        {
            _view.Hide(placed);
            Player.Focus?.Refresh();
        }
    }
}
