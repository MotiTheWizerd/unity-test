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
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The player while the world is played: an actor like any other (<see cref="Actor"/>, a <see cref="PlayedActor"/>
    /// built from one <see cref="PlayerProfile"/>: body, look, rules, equipment, weapons, combat) plus what only the
    /// player has: its controls, its camera (first or third person, <see cref="ViewSwitch"/>, put in place by the
    /// <see cref="CameraDirector"/>, closing in as a bow is drawn, <see cref="DrawZoom"/>, riding the look's eyes in
    /// first person, <see cref="PlayerEyes"/>), what it has in sight (<see cref="Focus"/>), what it carries in its
    /// hands (<see cref="ObjectCarry"/>) and in its pack (<see cref="Inventory"/>, weighed against its carry weight by
    /// <see cref="Burden"/>). <see cref="Spawn"/> stands it somewhere, takes the camera and captures the mouse;
    /// <see cref="Despawn"/> takes it all away again. The inventory's key opens and closes the inventory
    /// (<see cref="InventoryOpen"/>): while it is open the mouse is free for the screen and the player's world waits, as
    /// Skyrim pauses for its menus. It lives as long as the app, so the view chosen, the attributes, the look and what
    /// it carries carry over to the next spawn. What happens to the player is told on the game's stream
    /// (<see cref="Events"/>), which whoever runs the game's loop flushes (<see cref="GameSession"/>).
    /// </summary>
    public sealed class PlayerSession : IDisposable
    {
        readonly PlayerProfile _profile;
        readonly PlayerControls _controls;
        readonly CameraDirector _director;
        readonly DrawZoom _zoom;
        readonly PhysicsInteractionProbe _probe;
        GameObject _look;
        GameStage _stage;
        PlayedActor _actor;
        ObjectCarry _carry;
        AttributeRecord[] _attributeRecords;
        IDisposable _attributeEvents;

        /// <param name="camera">The camera the player sees through while spawned.</param>
        /// <param name="events">The game's stream, told what happens to the player.</param>
        public PlayerSession(PlayerProfile profile, Transform camera, GameEvents events)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            Events = events ?? throw new ArgumentNullException(nameof(events));
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

        /// <summary>The player as an actor of the game while spawned (its body, look, rules, weapons and combat); else null.</summary>
        public PlayedActor Actor => _actor;

        /// <summary>The player's rules while spawned (where it looks, how it moves), else null.</summary>
        public PlayerCharacter Player => _actor?.Rules;

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
        public WeaponReadiness Readiness => _actor?.Readiness;

        /// <summary>How the player fights (the attack keys swing each hand), while spawned; else null.</summary>
        public ActorCombat Combat => _actor?.Combat;

        /// <summary>Who can be struck in the game the player is spawned in, the player among them; else null.</summary>
        public Combatants Combatants => _stage?.Combatants;

        /// <summary>
        /// What the player carries against what it can, while spawned in a world with a carry weight; else null (it carries
        /// whatever it takes, unslowed).
        /// </summary>
        public Encumbrance Burden => _actor?.Burden;

        /// <summary>The world the player is spawned in (as played), which its items are read from; else null.</summary>
        public IRecordStore World => _stage?.World;

        /// <summary>What the player has in sight and what the use key would do, while spawned; else null.</summary>
        public InteractionFocus Focus { get; private set; }

        /// <summary>Whether the inventory is open: the mouse is the screen's and the player waits.</summary>
        public bool InventoryOpen { get; private set; }

        /// <summary>What the player looks like (a character prefab: the profile's until <see cref="ChangeLook"/>); null when it has no look.</summary>
        public GameObject Model => _look;

        /// <summary>The look as it stands on the body while spawned, with what it holds (for the editor's hold tuner); else null.</summary>
        public CharacterModel Character => _actor?.Model;

        /// <summary>The use key as a prompt shows it ("E").</summary>
        public string InteractKey => _controls.InteractKey;

        /// <summary>The inventory's key as a prompt shows it ("I").</summary>
        public string InventoryKey => _controls.InventoryKey;

        /// <summary>Opens the inventory, while spawned: the mouse is freed for it and the player waits.</summary>
        public void OpenInventory()
        {
            if (_actor == null || InventoryOpen)
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
            if (_actor != null)
                CaptureMouse(true);
        }

        /// <summary>
        /// Stands the player in <paramref name="stage"/>'s world at <paramref name="feet"/> (scene coordinates), facing
        /// <paramref name="heading"/>, and gives it the camera and the controls; the use key does what
        /// <paramref name="interactions"/> offer. The player keeps its numbers while the world's attribute records stay the
        /// same, and starts afresh from them when they changed.
        /// </summary>
        public void Spawn(Vector3 feet, float heading, GameStage stage, Interactions interactions)
        {
            if (stage == null)
                throw new ArgumentNullException(nameof(stage));

            Despawn();
            _stage = stage;
            UpdateAttributes(stage.World.OfType<AttributeRecord>());
            _actor = new PlayedActor(stage, ActorId.Player, "Player", _profile.Body, _profile.Locomotion(), _profile.EyeHeight,
                Attributes, Inventory, Equipment, "You") { Aim = () => _director.Pose };
            // In a fight the player walks unless the run key is held (a character's mind picks its own paces).
            _actor.Rules.Limits.Add(new CombatPace(stage.Modes, ActorId.Player));
            _actor.ChangeLook(_look);
            BindEyes();
            _carry = new ObjectCarry(_actor.Body.Collider, _profile.Carry());
            _actor.PlaceAt(feet, heading);
            Focus = new InteractionFocus(ActorId.Player, stage.World, interactions, _probe, _profile.Reach);
            Views.ThirdPerson.Begin();
            Views.FirstPerson.Begin();
            Frame(0f);

            // Dev: drawing as it spawns, the draw playing in the first second, saves a key while combat is tuned.
            if (_profile.StartDrawn)
                _actor.Readiness.Toggle();
            _controls.Enable();
            CaptureMouse(true);
        }

        /// <summary>
        /// Plays one frame: the inventory's key opens or closes the inventory, and while it is open the player waits.
        /// Otherwise the view takes its commands, and the player does what the controls say (<see cref="ActorCommands"/>:
        /// the ready-weapon key draws or sheathes; an attack key uses its hand, attacking or, with a shield, blocking
        /// while held; while something is carried, it throws it instead), the camera follows, then what is in sight is
        /// looked for from where the camera now is, so the prompt and the use key go by what this frame shows: a tap
        /// uses it, a hold grabs and carries it loose, the lift key held lifts it steady (<see cref="ObjectCarry"/>),
        /// letting go drops it.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_actor == null)
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
            _actor.Rules.Facing = Views.Current.Facing;
            var move = _controls.Read(deltaTime);
            var attackRight = _controls.ReadAttack(right: true);
            var attackLeft = _controls.ReadAttack(right: false);
            var throwing = _carry.IsCarrying && (attackRight || attackLeft);
            if (throwing)
                _carry.Throw(_director.Pose);
            _actor.Tick(new ActorCommands(move,
                readyWeapon: _controls.ReadReadyWeapon(),
                useRight: attackRight && !throwing,
                useLeft: attackLeft && !throwing,
                releaseRight: _controls.ReadAttackReleased(right: true),
                releaseLeft: _controls.ReadAttackReleased(right: false)), deltaTime);
            _zoom.Tick(_actor.Combat.DrawPull, deltaTime);
            _director.Magnification = _zoom.Magnification;
            Frame(deltaTime);
            Focus.Update(_director.Pose, _actor.Rules.Eyes);
            if (move.Activate)
                Focus.Activate();
            Carry(deltaTime);
        }

        // The use key held grabs what is in reach loose, the lift key held lifts it steady (CarryHold); letting go of the key
        // that took it drops it. Meanwhile it is held where the player looks.
        void Carry(float deltaTime)
        {
            var eyes = _actor.Rules.Eyes.ToUnity();
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
        public void Shift(Vector3 delta) => _actor?.Shift(delta);

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
            if (_actor == null)
                return;
            InventoryOpen = false;
            _controls.Disable();
            _carry.Dispose();
            _carry = null;
            Views.FirstPerson.Head = null;
            _actor.Dispose();
            _actor = null;
            _zoom.Reset();
            _director.Release();
            Focus = null;
            _stage = null;
            CaptureMouse(false);
        }

        /// <summary>
        /// The player looks like <paramref name="look"/> (a character prefab) from now on, kept between spawns. While spawned
        /// it changes where it stands, keeping all it is: what it carries, has equipped and has drawn shows on the new look
        /// (arrows still in flight are lost with the old one), and the first-person camera rides the new look's eyes.
        /// </summary>
        public void ChangeLook(GameObject look)
        {
            if (look == null)
                throw new ArgumentNullException(nameof(look));
            if (look == _look)
                return;
            _look = look;
            if (_actor == null)
                return;
            _actor.ChangeLook(look);
            BindEyes();
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

        // The first-person camera rides the eyes of the look the player has now, at its own eye height; none without a look.
        void BindEyes() =>
            Views.FirstPerson.Head = _actor.Model != null ? new PlayerEyes(_actor.Model, _actor.Rules.EyeHeight) : null;

        public void Dispose()
        {
            Despawn();
            Equipment.Dispose();
            _attributeEvents?.Dispose();
            _controls.Dispose();
        }

        // Captured, the mouse turns the look and hides; freed, it points at the screen.
        static void CaptureMouse(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        // The camera frames the player as the current view says, and the look is seen from within its head in first person.
        void Frame(float deltaTime)
        {
            var view = Views.Current;
            _actor.Rules.Facing = view.Facing;
            _director.Mode = view;
            _director.Tick(_actor.Rules, deltaTime);
            if (_actor.Model != null)
                _actor.Model.FirstPerson = view.InsideHead;
        }
    }
}
