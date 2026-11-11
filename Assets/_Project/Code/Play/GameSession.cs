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
using Forge.Game.Minds;
using Forge.Game.World;
using Forge.Play.Sound;
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
    /// played (<see cref="World"/>), what it gives every actor standing in it (<see cref="Stage"/>), what actors can do
    /// with the use key (<see cref="Interactions"/>: take items) and the player (<see cref="Player"/>). <see cref="Begin"/>
    /// gives the player its starting items and stands it in the world, <see cref="Tick"/> plays a frame
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

        readonly PlayerProfile _profile;
        readonly WorldView _view;
        readonly WorldFrame _frame;
        readonly ItemDropper _dropper;
        readonly ShotLandings _landings;
        readonly GroundSurfaces _surfaces;
        readonly SoundDirector _director;
        readonly SoundListener _listener;
        readonly IDisposable _fightCauses;
        readonly IDisposable _modeTrace;
        readonly IDisposable _deaths;

        /// <param name="camera">The camera the player sees through while playing, which the game's ears turn with (<see cref="SoundListener"/>).</param>
        /// <param name="view">The scene's world, which shows what playing changed.</param>
        /// <param name="frame">The world's frame in the scene, where the player's positions become the world's.</param>
        /// <param name="onError">Where a listener that fails is reported; the others still hear.</param>
        /// <param name="sound">How the game sounds; null plays it silent.</param>
        public GameSession(PlayerProfile profile, Transform camera, WorldView view, WorldFrame frame, Action<Exception> onError,
            SoundSettings sound = null)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Events = new GameEvents(onError);
            Changes = new WorldChanges();
            Combatants = new Combatants();
            PlacedActors = new PlacedActors(Combatants);
            Modes = new ActorModes(Events);
            _fightCauses = FightCauses.Bind(Modes, Events);
            // Dev: the console says each change of mode, while fights are tuned (the characters' minds act on them).
            _modeTrace = Events.Subscribe<ModeChanged>(e => Debug.Log($"[Forge] {e.Actor} is in {e.To} mode (was {e.From})."));
            _deaths = Events.Subscribe<ActorKilled>(OnKilled);
            // Silent or not, the scene has its ears (Unity wants one listener), at the camera until the player plays.
            _listener = new SoundListener(camera);
            if (sound != null)
                Sounds = new SoundPlayer(sound);
            _surfaces = new GroundSurfaces(view, frame, () => World);
            Player = new PlayerSession(profile, camera, Events);
            if (Sounds != null)
                _director = new SoundDirector(Events, Sounds, WhereIs, VoiceOf);
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

        /// <summary>What the game's sounds play on; null when it plays silent.</summary>
        public SoundPlayer Sounds { get; }

        /// <summary>What playing changed in the world, let go when playing ends.</summary>
        public WorldChanges Changes { get; }

        /// <summary>The world as played (as built, with <see cref="Changes"/> on top) while playing; else null.</summary>
        public IRecordStore World { get; private set; }

        /// <summary>What this game gives every actor standing in it (the player, the world's characters) while playing; else null.</summary>
        public GameStage Stage { get; private set; }

        /// <summary>The looks of the world's things, by asset key, for whatever shows them apart from the world (the inventory's preview).</summary>
        public IAssetCatalog Assets => _view.Assets;

        /// <summary>Who can be struck in this game: the player while spawned, the world's actors once met; emptied when it ends.</summary>
        public Combatants Combatants { get; }

        /// <summary>The world's placed actors (a training dummy) as combatants, by their placed objects.</summary>
        public PlacedActors PlacedActors { get; }

        /// <summary>What every actor is up to (at ease, fighting whom), the player's too; a blow starts a fight (<see cref="FightCauses"/>).</summary>
        public ActorModes Modes { get; }

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
            Stage = new GameStage(World, Events, Modes, _view.Assets, Combatants, PlacedActors, LandShot, Sounds, _surfaces);
            Bodies = new WorldPhysics(_view, World, OnSettled);
            // The world's characters stand on the player's body, each at its own height, until races give their own.
            Characters = new WorldCharacters(_view, Stage, _profile.Body, WanderSettings.Stroll);
            if (World.TryGet(CoreActors.Player, out var record) && record is ActorRecord player)
                StartingItems.Give(player, World, Player.Inventory, Player.Equipment);
            Player.Spawn(feet, heading, Stage, Interactions);
            Events.Flush();
        }

        /// <summary>The world's loose things as physical bodies while playing; else null.</summary>
        public WorldPhysics Bodies { get; private set; }

        /// <summary>The world's characters going about while playing; else null.</summary>
        public WorldCharacters Characters { get; private set; }

        /// <summary>Plays one frame, then tells what happened in it, heard from the player's head.</summary>
        public void Tick(float deltaTime)
        {
            Player.Tick(deltaTime);
            Modes.Tick(deltaTime);
            _listener.HearFrom(Player.Player?.Eyes.ToUnity());
            Bodies?.Tick();
            Characters?.Tick(deltaTime);
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
            var reach = Physics.SphereCast(hands, DropClearance, way, out var wall, DropReach, SceneLayers.World, QueryTriggerInteraction.Ignore)
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
            _listener.HearFrom(null);
            Bodies?.Dispose();
            Bodies = null;
            Characters?.Dispose();
            Characters = null;
            Player.Inventory.Clear();
            Modes.Clear();
            Combatants.Clear();
            PlacedActors.Clear();
            Changes.Clear();
            Stage = null;
            World = null;
            Events.Flush();
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); the player moves with it.</summary>
        public void Shift(Vector3 delta)
        {
            Player.Shift(delta);
            Bodies?.Shift(delta);
            Characters?.Shift(delta);
        }

        public void Dispose()
        {
            Changes.Added -= _view.ShowAdded;
            Changes.Moved -= _view.ShowMoved;
            Changes.Removed -= OnRemoved;
            Changes.Cleared -= _view.ShowAsBuilt;
            Player.Dispose();
            _deaths.Dispose();
            _modeTrace.Dispose();
            _fightCauses.Dispose();
            _director?.Dispose();
            Sounds?.Dispose();
            _listener.Dispose();
        }

        /// <summary>The actor <paramref name="actor"/> is as it stands in this game (the player, a character of the world); null when it doesn't stand in it.</summary>
        public PlayedActor ActorOf(ActorId actor) => Stage != null && Stage.Actors.TryGet(actor, out var standing) ? standing : null;

        // An actor's health ran out: a character falls and lies as it fell (its fight settled by FightCauses).
        // SHORTCUT: the player wakes up whole at once where it fell (Moti's call while fights are tested: "let it wake up
        // with full power, stop fighting"); its real death (a load, a menu) comes with the game.
        void OnKilled(ActorKilled e)
        {
            var actor = ActorOf(e.Actor);
            if (actor == null)
                return;
            if (e.Actor == ActorId.Player)
            {
                Revival.WakeUp(actor.Attributes);
                Debug.Log("[Forge] The player fell, and wakes up whole (dev).");
            }
            else
                actor.Fall();
        }

        Inventory InventoryOf(ActorId actor) => ActorOf(actor)?.Inventory;

        // Where an actor is in the scene: its feet, while it stands in the game.
        Vector3? WhereIs(ActorId actor) => ActorOf(actor)?.Rules.Feet.ToUnity();

        // The voice an actor's own sounds are made in: as the character it looks like has it.
        string VoiceOf(ActorId actor) => ActorOf(actor)?.Model is CharacterModel look ? CharacterVoice.Of(look.Root) : null;

        // A loose thing came to rest somewhere new (scene space): playing keeps it there, in the world's own coordinates.
        void OnSettled(RecordId id, Vector3 position, Quaternion rotation)
        {
            if (World != null && World.TryGet(id, out var record) && record is PlacedObjectRecord placed)
                Changes.Move(placed.WithTransform(_frame.ToWorld(position).ToNumerics(), rotation.ToNumerics(), placed.Scale));
        }

        // What was shot landed at a pose in the scene: it stays there in the world's own coordinates as the item it is,
        // to be taken back, unless it broke, stuck in what it struck (riding along with a loose thing, else fixed).
        void LandShot(Shot shot, Vector3 position, Quaternion rotation, Rigidbody stuckIn)
        {
            if (World == null)
                return;
            var placed = _landings.Land(shot, _view.Space, _frame.ToWorld(position).ToNumerics(), rotation.ToNumerics());
            if (placed != null)
                Bodies?.Stick(placed.Id, stuckIn);
        }

        // What was taken leaves the scene, and the player looks again at what is in sight.
        void OnRemoved(RecordId placed)
        {
            _view.Hide(placed);
            Player.Focus?.Refresh();
        }
    }
}
