using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Cameras;
using Forge.Game.Combat;
using Forge.Game.Equipment;
using Forge.Game.Events;
using Forge.Game.Interaction;
using Forge.Game.Items;
using Forge.Game.Player;
using Forge.Play.Sound;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The player while the world is played, all built from one <see cref="PlayerProfile"/>: its body
    /// (<see cref="CharacterControllerMotor"/>), its look (<see cref="PlayerAvatar"/>), its controls, its rules
    /// (<see cref="PlayerCharacter"/>), its camera (first or third person, <see cref="ViewSwitch"/>, put in place by the
    /// <see cref="CameraDirector"/>, closing in as a bow is drawn, <see cref="DrawZoom"/>), what it has in sight (<see cref="Focus"/>) and what it carries
    /// (<see cref="Inventory"/>, weighed against its carry weight by <see cref="Burden"/>). <see cref="Spawn"/> stands it
    /// somewhere, takes the camera and captures the mouse; <see cref="Despawn"/> takes it all away again. The inventory's
    /// key opens and closes the inventory (<see cref="InventoryOpen"/>): while it is open the mouse is free for the
    /// screen and the player's world waits, as Skyrim pauses for its menus. It lives as long as the app, so the view
    /// chosen, the attributes and what it carries carry over to the next spawn. What happens to the player is told on the
    /// game's stream (<see cref="Events"/>), which whoever runs the game's loop flushes (<see cref="GameSession"/>).
    /// </summary>
    public sealed class PlayerSession : IDisposable
    {
        readonly PlayerProfile _profile;
        readonly PlayerControls _controls;
        readonly CameraDirector _director;
        readonly DrawZoom _zoom;
        readonly PhysicsInteractionProbe _probe;
        CharacterControllerMotor _motor;
        GameObject _look;
        PlayerAvatar _avatar;
        AttributeRecord[] _attributeRecords;
        IDisposable _attributeEvents;
        EquipmentModifiers _equipmentModifiers;
        EquipmentView _equipmentView;
        ReadinessView _readinessView;
        CombatView _combatView;
        BladeSweep _sweep;
        ShotView _shotView;
        ArrowFlights _arrows;
        ObjectCarry _carry;
        FootstepView _footsteps;
        readonly IAssetCatalog _assets;
        readonly Combatants _combatants;
        readonly PlacedActors _placedActors;
        readonly ArrowFlights.Landing _landShot;
        readonly SoundPlayer _sounds;
        readonly GroundSurfaces _surfaces;

        /// <param name="camera">The camera the player sees through while spawned.</param>
        /// <param name="events">The game's stream, told what happens to the player.</param>
        /// <param name="assets">The looks of the world's things, for what the player holds.</param>
        /// <param name="combatants">Who can be struck in the game; the player is one while spawned.</param>
        /// <param name="placedActors">The world's placed actors, which the player's blows find.</param>
        /// <param name="landShot">Where what the player shoots goes once it lands, with its pose there and what it is in.</param>
        /// <param name="sounds">What the player's sounds (its footsteps) play on; null: it makes none.</param>
        /// <param name="surfaces">What is underfoot, which its steps sound of; null: nothing in particular.</param>
        public PlayerSession(PlayerProfile profile, Transform camera, GameEvents events, IAssetCatalog assets, Combatants combatants,
            PlacedActors placedActors, ArrowFlights.Landing landShot, SoundPlayer sounds = null, GroundSurfaces surfaces = null)
        {
            _sounds = sounds;
            _surfaces = surfaces;
            _landShot = landShot ?? throw new ArgumentNullException(nameof(landShot));
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _placedActors = placedActors ?? throw new ArgumentNullException(nameof(placedActors));
            _controls = new PlayerControls(profile);
            _look = profile.Model;
            Views = new ViewSwitch(
                new FirstPersonCamera(profile.FirstPerson()),
                new ThirdPersonCamera(profile.ThirdPerson(), new PhysicsCameraObstacles()),
                profile.StartInThirdPerson);
            _director = new CameraDirector(camera, Views.Current);
            _zoom = profile.DrawZoom();
            _probe = new PhysicsInteractionProbe(profile.AimRadius);
            Equipment = new ActorEquipment(ActorId.Player, Inventory, events);
        }

        /// <summary>First or third person; kept between spawns.</summary>
        public ViewSwitch Views { get; }

        /// <summary>The game's stream, which the player's happenings go on (named <see cref="ActorId.Player"/>).</summary>
        public GameEvents Events { get; }

        /// <summary>The player while spawned, else null.</summary>
        public PlayerCharacter Player { get; private set; }

        /// <summary>
        /// The player's attributes (health, stamina, ...), kept from one spawn to the next, as a game keeps them; null
        /// until a spawn gives the world's attributes.
        /// </summary>
        public ActorAttributes Attributes { get; private set; }

        /// <summary>What the player carries, kept from one spawn to the next; the game empties it when it puts the world back (<see cref="GameSession.End"/>).</summary>
        public Inventory Inventory { get; } = new Inventory();

        /// <summary>
        /// What the player has equipped, from what it carries (one-handed things in either hand, or both); kept from one
        /// spawn to the next with the pack; what it does to the player's attributes applies while spawned.
        /// </summary>
        public ActorEquipment Equipment { get; }

        /// <summary>Whether the player's weapons are drawn (R), while spawned; else null. Every spawn starts sheathed, unless the profile says it draws (<see cref="PlayerProfile.StartDrawn"/>).</summary>
        public WeaponReadiness Readiness { get; private set; }

        /// <summary>How the player fights (the attack keys swing each hand), while spawned; else null.</summary>
        public ActorCombat Combat { get; private set; }

        /// <summary>Who can be struck in the game, the player among them while spawned.</summary>
        public Combatants Combatants => _combatants;

        /// <summary>
        /// What the player carries against what it can, while spawned in a world with a carry weight; else null (it carries
        /// whatever it takes, unslowed).
        /// </summary>
        public Encumbrance Burden { get; private set; }

        /// <summary>The world the player is spawned in (as played), which its items are read from; else null.</summary>
        public IRecordStore World { get; private set; }

        /// <summary>What the player has in sight and what the use key would do, while spawned; else null.</summary>
        public InteractionFocus Focus { get; private set; }

        /// <summary>Whether the inventory is open: the mouse is the screen's and the player waits.</summary>
        public bool InventoryOpen { get; private set; }

        /// <summary>What the player looks like (a character prefab: the profile's until <see cref="ChangeLook"/>); null when it has no look.</summary>
        public GameObject Model => _look;

        /// <summary>The look as it stands on the body while spawned, with what it holds (for the editor's hold tuner); else null.</summary>
        public CharacterModel Character => _avatar?.Model;

        /// <summary>The use key as a prompt shows it ("E").</summary>
        public string InteractKey => _controls.InteractKey;

        /// <summary>The inventory's key as a prompt shows it ("I").</summary>
        public string InventoryKey => _controls.InventoryKey;

        /// <summary>Opens the inventory, while spawned: the mouse is freed for it and the player waits.</summary>
        public void OpenInventory()
        {
            if (Player == null || InventoryOpen)
                return;
            _carry.Drop();
            InventoryOpen = true;
            CaptureMouse(false);
        }

        /// <summary>Closes the inventory: the mouse looks again and the player plays on.</summary>
        public void CloseInventory()
        {
            if (!InventoryOpen)
                return;
            InventoryOpen = false;
            if (Player != null)
                CaptureMouse(true);
        }

        /// <summary>
        /// Stands the player in <paramref name="world"/> at <paramref name="feet"/> (scene coordinates), facing
        /// <paramref name="heading"/>, and gives it the camera and the controls; the use key does what
        /// <paramref name="interactions"/> offer. The player keeps its numbers while the world's attribute records stay the
        /// same, and starts afresh from them when they changed.
        /// </summary>
        public void Spawn(Vector3 feet, float heading, IRecordStore world, Interactions interactions)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            Despawn();
            UpdateAttributes(world.OfType<AttributeRecord>());
            _motor = new CharacterControllerMotor(_profile);
            Player = new PlayerCharacter(_motor, _profile.Locomotion(), _profile.EyeHeight, Attributes, Events);
            World = world;
            // A world made before carrying weighed anything (no carry weight) lets the player carry anything.
            if (Attributes != null && Attributes.Set.Contains(CoreAttributes.CarryWeight))
            {
                Burden = new Encumbrance(Inventory, world, Attributes, Events);
                Player.Limits.Add(Burden);
            }

            if (Attributes != null)
                _equipmentModifiers = new EquipmentModifiers(Equipment, Attributes);
            Readiness = new WeaponReadiness(Equipment, world, Events);
            Combat = new ActorCombat(Equipment, Readiness, world, Attributes, _combatants, Events) { Facing = BodyForward };
            if (Attributes != null)
                _combatants.Add(ActorId.Player, Attributes, "You", Combat);
            ShowLook();

            _carry = new ObjectCarry(_motor.Collider, _profile.Carry());
            Player.PlaceAt(feet.ToNumerics(), heading);
            Focus = new InteractionFocus(ActorId.Player, world, interactions, _probe, _profile.Reach);
            Views.ThirdPerson.Begin();
            Views.FirstPerson.Begin();
            Frame(0f);

            // Dev: drawing as it spawns, the draw playing in the first second, saves a key while combat is tuned.
            if (_profile.StartDrawn)
                Readiness.Toggle();
            _controls.Enable();
            CaptureMouse(true);
        }

        /// <summary>
        /// Plays one frame: the inventory's key opens or closes the inventory, and while it is open the player waits.
        /// Otherwise the view and the player take their commands (the ready-weapon key draws or sheathes; an attack key uses
        /// its hand, attacking or, with a shield, blocking while held; while something is carried, it throws it), the camera and the model follow, then what is in sight is looked for from where
        /// the camera now is, so the prompt and the use key go by what this frame shows: a tap uses it, a hold grabs and
        /// carries it loose, the lift key held lifts it steady (<see cref="ObjectCarry"/>), letting go drops it.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (Player == null)
                return;
            if (_controls.ReadInventory())
            {
                if (InventoryOpen)
                    CloseInventory();
                else
                    OpenInventory();
            }

            if (InventoryOpen)
                return;

            if (_controls.ReadNextLook())
                NextLook();
            Views.Apply(_controls.ReadView());
            Player.Facing = Views.Current.Facing;
            var commands = _controls.Read(deltaTime);
            if (_controls.ReadReadyWeapon())
                Readiness.Toggle();
            Readiness.Tick(deltaTime);
            var attackRight = _controls.ReadAttack(right: true);
            var attackLeft = _controls.ReadAttack(right: false);
            if (_carry.IsCarrying && (attackRight || attackLeft))
                _carry.Throw(_director.Pose);
            else
            {
                if (attackRight)
                    Combat.TryUse(CoreEquipSlots.RightHand);
                if (attackLeft)
                    Combat.TryUse(CoreEquipSlots.LeftHand);
            }

            if (_controls.ReadAttackReleased(right: true))
                Combat.Release(CoreEquipSlots.RightHand);
            if (_controls.ReadAttackReleased(right: false))
                Combat.Release(CoreEquipSlots.LeftHand);
            Combat.Tick(deltaTime);
            _arrows?.Tick(deltaTime);
            _zoom.Tick(Combat.DrawPull, deltaTime);
            _director.Magnification = _zoom.Magnification;
            Player.Aiming = Combat.Aiming;
            Player.Tick(commands, deltaTime);
            Frame(deltaTime);
            _sweep?.Tick();
            _footsteps?.Tick(Player.Motion, deltaTime);
            Focus.Update(_director.Pose, Player.Eyes);
            if (commands.Activate)
                Focus.Activate();
            Carry(deltaTime);
        }

        // The use key held grabs what is in reach loose, the lift key held lifts it steady (CarryHold); letting go of the key
        // that took it drops it. Meanwhile it is held where the player looks.
        void Carry(float deltaTime)
        {
            var eyes = Player.Eyes.ToUnity();
            if (_controls.Use == UseKeyEvent.HoldStarted)
                _carry.TryGrab(_director.Pose, eyes, CarryHold.Loose);
            else if (_controls.Use == UseKeyEvent.HoldEnded)
                _carry.Drop(CarryHold.Loose);
            if (_controls.ReadLift())
                _carry.TryGrab(_director.Pose, eyes, CarryHold.Steady);
            else if (_controls.ReadLiftReleased())
                _carry.Drop(CarryHold.Steady);
            _carry.Hold(_director.Pose, eyes, deltaTime);
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); the player moves with it.</summary>
        public void Shift(Vector3 delta)
        {
            Player?.Shift(delta.ToNumerics());
            _arrows?.Shift(delta);
            _footsteps?.Shift(delta);
        }

        // Records are immutable, so the same records (by reference) are the same attributes.
        void UpdateAttributes(IEnumerable<AttributeRecord> attributes)
        {
            var records = (attributes ?? throw new ArgumentNullException(nameof(attributes))).ToArray();
            if (_attributeRecords != null && records.Length == _attributeRecords.Length && !records.Except(_attributeRecords).Any())
                return;
            _attributeRecords = records;
            _attributeEvents?.Dispose();
            _attributeEvents = null;
            // A world without attributes (one made before them) plays as before: moving costs nothing.
            Attributes = records.Length > 0 ? new ActorAttributes(AttributeSet.From(records), ActorId.Player) : null;
            if (Attributes != null)
                _attributeEvents = AttributeEvents.Bind(Attributes, Events);
        }

        /// <summary>Takes the player away and gives the mouse back; the camera is the caller's again.</summary>
        public void Despawn()
        {
            if (Player == null)
                return;
            InventoryOpen = false;
            _controls.Disable();
            _carry?.Dispose();
            _carry = null;
            HideLook();
            Combat?.Dispose();
            Combat = null;
            _combatants.Remove(ActorId.Player);
            _zoom.Reset();
            _director.Release();
            _motor.Dispose();
            _motor = null;
            Burden?.Dispose();
            Burden = null;
            _equipmentModifiers?.Dispose();
            _equipmentModifiers = null;
            Readiness?.Dispose();
            Readiness = null;
            Player = null;
            World = null;
            Focus = null;
            CaptureMouse(false);
        }

        /// <summary>
        /// The player looks like <paramref name="look"/> (a character prefab) from now on, kept between spawns. While spawned
        /// it changes where it stands, keeping all it is: what it carries, has equipped and has drawn shows on the new look
        /// (arrows still in flight are lost with the old one).
        /// </summary>
        public void ChangeLook(GameObject look)
        {
            if (look == null)
                throw new ArgumentNullException(nameof(look));
            if (look == _look)
                return;
            _look = look;
            if (Player == null)
                return;
            HideLook();
            ShowLook();
            Views.FirstPerson.Begin();
            Frame(0f);
        }

        /// <summary>
        /// The next of the profile's looks (<see cref="PlayerProfile.Looks"/>, the first again after the last), for the
        /// next-look key: a dev tool while animation and combat are tuned, until a character picker replaces it.
        /// </summary>
        public void NextLook()
        {
            var looks = _profile.Looks;
            var next = 0;
            for (var i = 0; i < looks.Count; i++)
            {
                if (looks[i] == _look)
                    next = (i + 1) % looks.Count;
            }

            // None to switch to, or one gone from the project since the list was made.
            if (next >= looks.Count || looks[next] == null || looks[next] == _look)
                return;
            ChangeLook(looks[next]);
            Debug.Log($"[Forge] The player is {_look.name} now (F8: the next).");
        }

        // The look on the body and what shows on it: what is equipped hangs from it; the weapons' readiness, the swings,
        // the shots and the steps play on it; the first-person camera rides its eyes, and the player sees from their height.
        void ShowLook()
        {
            Player.EyeHeight = _profile.EyeHeight;
            if (_look == null)
                return;
            _avatar = new PlayerAvatar(_motor.Body, _look, _motor.Hover, _profile.EyeHeight);
            Player.EyeHeight = _avatar.EyeHeight;
            _equipmentView = new EquipmentView(Equipment, _avatar.Model, World, _assets);
            _readinessView = new ReadinessView(Readiness, _avatar.Model);
            _combatView = new CombatView(Combat, _avatar.Model);
            _sweep = new BladeSweep(Combat, _avatar.Model, World, Identify);
            _shotView = new ShotView(Combat, Equipment, _avatar.Model, _assets, () => _director.Pose);
            _arrows = new ArrowFlights(Combat, _avatar.Model, _assets, () => _director.Pose, Identify, _landShot);
            if (_sounds != null)
                _footsteps = new FootstepView(_avatar.Model, _sounds, _surfaces, Player.Locomotion.Settings);
            Views.FirstPerson.Head = _avatar;
        }

        void HideLook()
        {
            _footsteps = null;
            _equipmentView?.Dispose();
            _equipmentView = null;
            _readinessView?.Dispose();
            _readinessView = null;
            _sweep?.Dispose();
            _sweep = null;
            _shotView?.Dispose();
            _shotView = null;
            _arrows?.Dispose();
            _arrows = null;
            _combatView?.Dispose();
            _combatView = null;
            Views.FirstPerson.Head = null;
            _avatar?.Dispose();
            _avatar = null;
        }

        public void Dispose()
        {
            Despawn();
            Equipment.Dispose();
            _attributeEvents?.Dispose();
            _controls.Dispose();
        }

        // Whom a collider the player's blade passed through belongs to: a placed actor of the world, by its placed object.
        ActorId? Identify(Collider collider)
        {
            var link = collider.GetComponentInParent<RecordLink>();
            return link != null && World != null && _placedActors.TryGet(World, link.RecordId, out var actor) ? actor : (ActorId?)null;
        }

        // Which way the player's body faces, level (what its guard covers); straight ahead of the scene while unspawned.
        System.Numerics.Vector3 BodyForward()
        {
            var forward = Player != null ? Forge.Core.Geometry.Compass.Forward(Player.BodyHeading) : System.Numerics.Vector2.UnitY;
            return new System.Numerics.Vector3(forward.X, 0f, forward.Y);
        }

        // Captured, the mouse turns the look and hides; freed, it points at the screen.
        static void CaptureMouse(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        // The camera frames the player as the current view says; the model turns with the body, is seen from within its head in first person, and animates as the body moved.
        void Frame(float deltaTime)
        {
            var view = Views.Current;
            Player.Facing = view.Facing;
            _director.Mode = view;
            _director.Tick(Player, deltaTime);
            if (_avatar == null)
                return;
            _avatar.Face(Player.BodyHeading);
            _avatar.FirstPerson = view.InsideHead;
            if (deltaTime > 0f)
                _avatar.Animate(Player.Motion, deltaTime);
        }
    }
}
