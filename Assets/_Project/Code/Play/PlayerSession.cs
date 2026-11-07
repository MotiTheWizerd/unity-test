using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Cameras;
using Forge.Game.Events;
using Forge.Game.Interaction;
using Forge.Game.Items;
using Forge.Game.Player;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The player while the world is played, all built from one <see cref="PlayerProfile"/>: its body
    /// (<see cref="CharacterControllerMotor"/>), its look (<see cref="PlayerAvatar"/>), its controls, its rules
    /// (<see cref="PlayerCharacter"/>), its camera (first or third person, <see cref="ViewSwitch"/>, put in place by the
    /// <see cref="CameraDirector"/>), what it has in sight (<see cref="Focus"/>) and what it carries
    /// (<see cref="Inventory"/>). <see cref="Spawn"/> stands it somewhere, takes the camera and captures the mouse;
    /// <see cref="Despawn"/> takes it all away again. It lives as long as the app, so the view chosen, the attributes and
    /// what it carries carry over to the next spawn. What happens to the player is told on the game's stream
    /// (<see cref="Events"/>), which whoever runs the game's loop flushes (<see cref="GameSession"/>).
    /// </summary>
    public sealed class PlayerSession : IDisposable
    {
        readonly PlayerProfile _profile;
        readonly PlayerControls _controls;
        readonly CameraDirector _director;
        readonly PhysicsInteractionProbe _probe;
        CharacterControllerMotor _motor;
        PlayerAvatar _avatar;
        AttributeRecord[] _attributeRecords;
        IDisposable _attributeEvents;

        /// <param name="camera">The camera the player sees through while spawned.</param>
        /// <param name="events">The game's stream, told what happens to the player.</param>
        public PlayerSession(PlayerProfile profile, Transform camera, GameEvents events)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            _controls = new PlayerControls(profile);
            Views = new ViewSwitch(
                new FirstPersonCamera(),
                new ThirdPersonCamera(profile.ThirdPerson(), new PhysicsCameraObstacles()),
                profile.StartInThirdPerson);
            _director = new CameraDirector(camera, Views.Current);
            _probe = new PhysicsInteractionProbe(profile.AimRadius);
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

        /// <summary>What the player carries, kept from one spawn to the next, so it keeps what it took when the world resets.</summary>
        public Inventory Inventory { get; } = new Inventory();

        /// <summary>What the player has in sight and what the use key would do, while spawned; else null.</summary>
        public InteractionFocus Focus { get; private set; }

        /// <summary>The use key as a prompt shows it ("E").</summary>
        public string InteractKey => _controls.InteractKey;

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
            if (_profile.Model != null)
                _avatar = new PlayerAvatar(_motor.Body, _profile.Model);
            Player = new PlayerCharacter(_motor, _profile.Locomotion(), _profile.EyeHeight, Attributes, Events);
            Player.PlaceAt(feet.ToNumerics(), heading);
            Focus = new InteractionFocus(ActorId.Player, world, interactions, _probe, _profile.Reach);
            Views.ThirdPerson.Begin();
            Frame(0f);

            _controls.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Plays one frame: the view and the player take their commands, the camera and the model follow, then what is in
        /// sight is looked for from where the camera now is, so the prompt and the use key go by what this frame shows.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (Player == null)
                return;
            Views.Apply(_controls.ReadView());
            Player.Facing = Views.Current.Facing;
            var commands = _controls.Read();
            Player.Tick(commands, deltaTime);
            Frame(deltaTime);
            Focus.Update(_director.Pose, Player.Eyes);
            if (commands.Activate)
                Focus.Activate();
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); the player moves with it.</summary>
        public void Shift(Vector3 delta)
        {
            Player?.Shift(delta.ToNumerics());
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
            _controls.Disable();
            _avatar?.Dispose();
            _avatar = null;
            _motor.Dispose();
            _motor = null;
            Player = null;
            Focus = null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Dispose()
        {
            Despawn();
            _attributeEvents?.Dispose();
            _controls.Dispose();
        }

        // The camera frames the player as the current view says; the model turns with the body, shows unless the camera is inside it, and animates as the body moved.
        void Frame(float deltaTime)
        {
            var view = Views.Current;
            Player.Facing = view.Facing;
            _director.Mode = view;
            _director.Tick(Player, deltaTime);
            if (_avatar == null)
                return;
            _avatar.Face(Player.BodyHeading);
            _avatar.Show(view.ShowsBody);
            if (deltaTime > 0f)
                _avatar.Animate(Player.Motion, deltaTime);
        }
    }
}
