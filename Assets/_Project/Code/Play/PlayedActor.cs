using System;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Cameras;
using Forge.Game.Combat;
using Forge.Game.Equipment;
using Forge.Game.Items;
using Forge.Game.Motion;
using Forge.Game.Player;
using Forge.Play.Sound;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// One actor standing in a played world, the player or a character of the world, made of the same parts whoever
    /// minds it: a body (<see cref="Body"/>, an <see cref="ActorBody"/> of its <see cref="BodyShape"/>), the rules moving
    /// it (<see cref="Rules"/>: gravity, slopes, steps, crouching, what moving costs its attributes), what it carries and
    /// has equipped (<see cref="Inventory"/>, <see cref="Equipment"/>: lent by whoever keeps them, the player's from one
    /// spawn to the next; what is worn modifies its attributes while it stands, and its load weighs on it,
    /// <see cref="Burden"/>), whether its weapons are drawn (<see cref="Readiness"/>), how it fights
    /// (<see cref="Combat"/>: one who can be struck among the game's combatants, its body found by blows and arrows
    /// through its <see cref="ActorLink"/>, its guard covering the way its body faces), and its look (<see cref="Model"/>,
    /// changed at will: <see cref="ChangeLook"/>) with everything shown on it:
    /// what it holds, its draws and sheathes, its swings, the blade's sweep through the scene, its shots and their
    /// flight, its footsteps. Each frame it does what it is told (<see cref="Tick"/>, <see cref="ActorCommands"/>) and
    /// shows it. Only who tells it differs: the player's controls (<see cref="PlayerSession"/>), a mind
    /// (<see cref="WorldCharacters"/>). It stands on the game's roster (<see cref="ActorRoster"/>) from the moment it is
    /// made; disposed, it leaves, body and look go, and it can be struck no more.
    /// </summary>
    public sealed class PlayedActor : IDisposable
    {
        readonly float _eyeHeight;
        readonly EquipmentModifiers _equipmentModifiers;
        string _deathMove;
        CharacterModel _model;
        EquipmentView _equipmentView;
        ReadinessView _readinessView;
        CombatView _combatView;
        BladeSweep _sweep;
        ShotView _shotView;
        ArrowFlights _arrows;
        FootstepView _footsteps;

        /// <param name="stage">The game it stands in.</param>
        /// <param name="name">Its body's scene object's name ("Player", the character's).</param>
        /// <param name="eyeHeight">Meters from the feet to the eyes, for a look that can't tell (no head); a look with a head says itself.</param>
        /// <param name="attributes">Its numbers (health, stamina, ...), kept by whoever owns it; null for an actor without any: nothing costs it, and nothing can strike it.</param>
        /// <param name="inventory">What it carries, kept by whoever owns it.</param>
        /// <param name="equipment">What it has equipped, from what it carries; kept likewise.</param>
        /// <param name="displayName">What a player reads of it ("You", "Viking Woman"); null: its id.</param>
        public PlayedActor(GameStage stage, ActorId id, string name, BodyShape shape, LocomotionSettings locomotion, float eyeHeight,
            ActorAttributes attributes, Inventory inventory, ActorEquipment equipment, string displayName = null)
        {
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            if (id.IsDefault)
                throw new ArgumentException("An actor has an id.", nameof(id));
            Id = id;
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            Attributes = attributes;
            _eyeHeight = eyeHeight;

            Body = new ActorBody(name, shape, id);
            Rules = new PlayerCharacter(Body, locomotion, eyeHeight, attributes, stage.Events);
            // A world made before carrying weighed anything (no carry weight) lets it carry anything.
            if (attributes != null && attributes.Set.Contains(CoreAttributes.CarryWeight))
            {
                Burden = new Encumbrance(inventory, stage.World, attributes, stage.Events);
                Rules.Limits.Add(Burden);
            }

            if (attributes != null)
                _equipmentModifiers = new EquipmentModifiers(equipment, attributes);
            Readiness = new WeaponReadiness(equipment, stage.World, stage.Events);
            Combat = new ActorCombat(equipment, Readiness, stage.World, attributes, stage.Combatants, stage.Events) { Facing = BodyForward };
            if (attributes != null)
                stage.Combatants.Add(id, attributes, displayName, Combat);
            stage.Actors.Add(this);
        }

        /// <summary>The game it stands in.</summary>
        public GameStage Stage { get; }

        /// <summary>Who it is, which its happenings are told as.</summary>
        public ActorId Id { get; }

        /// <summary>Its body: the capsule that stands in the world, which its look rides on.</summary>
        public ActorBody Body { get; }

        /// <summary>The rules moving it: where it looks, how it moves, which way its body faces.</summary>
        public PlayerCharacter Rules { get; }

        /// <summary>Its attributes (health, stamina, ...); null for an actor without any.</summary>
        public ActorAttributes Attributes { get; }

        /// <summary>What it carries.</summary>
        public Inventory Inventory { get; }

        /// <summary>What it has equipped, from what it carries.</summary>
        public ActorEquipment Equipment { get; }

        /// <summary>Whether its weapons are drawn, drawing, sheathed or going back.</summary>
        public WeaponReadiness Readiness { get; }

        /// <summary>How it fights: each hand's swings, its shots, its guard.</summary>
        public ActorCombat Combat { get; }

        /// <summary>What it carries against what it can; null in a world without a carry weight (it carries whatever it takes, unslowed).</summary>
        public Encumbrance Burden { get; }

        /// <summary>Its look as it stands on the body, with what it holds; null while it has none.</summary>
        public CharacterModel Model => _model;

        /// <summary>The character prefab it looks like now; null for no look.</summary>
        public GameObject Look { get; private set; }

        /// <summary>
        /// Where it aims from and which way (what its shots fly at, what its draw turns to): the player's camera; until
        /// set, its own eyes along its look.
        /// </summary>
        public Func<ViewPose> Aim { get; set; }

        /// <summary>Whether it has fallen (<see cref="Fall"/>): it lies where it fell and does nothing until it <see cref="Rise"/>s.</summary>
        public bool Fallen { get; private set; }

        /// <summary>
        /// It falls (its health ran out): its swings stop, its body lies down (<see cref="ActorBody.Lie"/>: no obstacle,
        /// struck by nothing) and its look dies in its weapon's style's death (<see cref="ActorCombat.DeathAnimation"/>,
        /// else the look's own; one of the death's variants by chance, <see cref="CharacterModel.Vary"/>) and stays so;
        /// nothing moves it, nothing costs it, and its look stays down, until it rises.
        /// </summary>
        public void Fall()
        {
            if (Fallen)
                return;
            Fallen = true;
            Combat.Stop();
            Body.Lie(true);
            var death = Combat.DeathAnimation ?? HumanoidAnimation.DeathMove;
            _deathMove = _model != null ? _model.Vary(death) : death;
            _model?.PlayThrough(_deathMove);
        }

        /// <summary>It gets up again (revived): its body stands where it lay, its look rises out of its death, and it is itself again.</summary>
        public void Rise()
        {
            if (!Fallen)
                return;
            Fallen = false;
            Body.Lie(false);
            if (_deathMove != null)
                _model?.Resume(_deathMove, 1f);
            _deathMove = null;
        }

        /// <summary>Stands it at <paramref name="feet"/> (scene coordinates), body and look facing <paramref name="heading"/>, at rest.</summary>
        public void PlaceAt(Vector3 feet, float heading)
        {
            Rules.PlaceAt(feet.ToNumerics(), heading);
            Show(0f);
        }

        /// <summary>
        /// It looks like <paramref name="prefab"/> (a character prefab; null: nothing) from now on, where it stands: the
        /// look and everything shown on it are built anew, keeping all it is (what it carries, has equipped and has
        /// drawn shows on the new look; arrows still in flight are lost with the old one), and its eyes are the new
        /// look's own.
        /// </summary>
        public void ChangeLook(GameObject prefab)
        {
            HideLook();
            Look = prefab;
            if (prefab == null)
                return;

            _model = new CharacterModel(Body.Body, prefab, Body.Hover);
            Rules.EyeHeight = _model.TryGetEyeHeight(out var eyes) ? eyes : _eyeHeight;
            _equipmentView = new EquipmentView(Equipment, _model, Stage.World, Stage.Assets);
            _readinessView = new ReadinessView(Readiness, _model);
            _combatView = new CombatView(Combat, _model);
            _sweep = new BladeSweep(Combat, _model, Stage.World, Stage.Identify);
            _shotView = new ShotView(Combat, Equipment, _model, Stage.Assets, AimPose);
            _arrows = new ArrowFlights(Combat, _model, Stage.Assets, AimPose, Stage.Identify, Stage.LandShot);
            if (Stage.Sounds != null)
                _footsteps = new FootstepView(_model, Stage.Sounds, Stage.Surfaces, Rules.Locomotion.Settings);
            Show(0f);
            // Fallen, the new look lies as the old one did: at the end of its death.
            if (Fallen && _deathMove != null)
                _model.Hold(_deathMove, 1f);
        }

        /// <summary>
        /// Does what it is told for one frame of <paramref name="deltaTime"/> seconds, in order: the weapons drawn or put
        /// away, each hand used or let go of, the swings and shots carried on, then the move made (squaring up to the
        /// aim while a bow is drawn), and all of it shown on the look: the body turned, the stride, the blade's sweep,
        /// the steps. Fallen, it lies still whatever it is told.
        /// </summary>
        public void Tick(ActorCommands commands, float deltaTime)
        {
            if (Fallen)
            {
                // Down: whatever it is told, it lies still; its shots fly on.
                _arrows?.Tick(deltaTime);
                _model?.Animate(BodyMotion.Still, deltaTime);
                return;
            }

            if (commands.ReadyWeapon)
                Readiness.Toggle();
            Readiness.Tick(deltaTime);
            if (commands.UseRight)
                Combat.TryUse(CoreEquipSlots.RightHand);
            if (commands.UseLeft)
                Combat.TryUse(CoreEquipSlots.LeftHand);
            if (commands.ReleaseRight)
                Combat.Release(CoreEquipSlots.RightHand);
            if (commands.ReleaseLeft)
                Combat.Release(CoreEquipSlots.LeftHand);
            Combat.Tick(deltaTime);
            _arrows?.Tick(deltaTime);
            Rules.Aiming = Combat.Aiming;
            Rules.Tick(commands.Move, deltaTime);
            Show(deltaTime);
            _sweep?.Tick();
            _footsteps?.Tick(Rules.Motion, deltaTime);
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); it moves with it, mid-fall, mid-shot or not.</summary>
        public void Shift(Vector3 delta)
        {
            Rules.Shift(delta.ToNumerics());
            _arrows?.Shift(delta);
            _footsteps?.Shift(delta);
        }

        public void Dispose()
        {
            Stage.Actors.Remove(this);
            HideLook();
            Combat.Dispose();
            Stage.Combatants.Remove(Id);
            Body.Dispose();
            Burden?.Dispose();
            _equipmentModifiers?.Dispose();
            Readiness.Dispose();
        }

        // The look turned as the body faces, showing how the body moved over the frame.
        void Show(float deltaTime)
        {
            if (_model == null)
                return;
            _model.Face(Rules.BodyHeading);
            _model.Animate(Rules.Motion, deltaTime);
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
            _model?.Dispose();
            _model = null;
            Look = null;
            Rules.EyeHeight = _eyeHeight;
        }

        // Where it aims from and which way: as set (the player's camera), else from its eyes along its look.
        ViewPose AimPose() => Aim != null ? Aim() : new ViewPose(Rules.Eyes, FirstPersonCamera.Looking(Rules.Look));

        // Which way its body faces, level (what its guard covers).
        System.Numerics.Vector3 BodyForward()
        {
            var forward = Forge.Core.Geometry.Compass.Forward(Rules.BodyHeading);
            return new System.Numerics.Vector3(forward.X, 0f, forward.Y);
        }
    }
}
