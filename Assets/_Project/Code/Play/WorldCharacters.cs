using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Combat;
using Forge.Game.Equipment;
using Forge.Game.Items;
using Forge.Game.Minds;
using Forge.Game.Motion;
using Forge.Game.Player;
using Forge.World;
using UnityEngine;
// Not UnityEngine.Compass, the device's magnetometer.
using Compass = Forge.Core.Geometry.Compass;
using Random = System.Random;

namespace Forge.Play
{
    /// <summary>
    /// The world's characters (a placed object whose look is an animated humanoid: the NPCs of the catalog) going about
    /// while it plays, each an actor as the player is (<see cref="PlayedActor"/>: a body of its own, of the humanoid
    /// <see cref="BodyShape"/> at its own height, its look riding on it, the same rules moving it), and who its base
    /// object says (<see cref="BaseObjectRecord.Actor"/>, an <see cref="ActorRecord"/>: its name, the world's
    /// attributes as its own numbers, so a blow hurts it, and what it starts out carrying and holding,
    /// <see cref="StartingItems"/>: a sword and a shield, put away), told what to do by its mind (<see cref="IMind"/>)
    /// rather than by keys: someone's, a <see cref="Fighter"/> that strolls about where it was placed
    /// (<see cref="Wander"/>) and fights back whoever strikes it; no one's, a <see cref="Wander"/> alone. Its head turns
    /// to whom its eyes settle on (<see cref="Gaze"/>, <see cref="PlayedActor.LooksAt"/>): the foe it fights, else the
    /// player passing near. Its body
    /// stands in the player's way and the player in its. While it goes about, the placed object it was stands hidden;
    /// bound for one game, disposed, every body goes and every placed object shows again where its record has it.
    /// </summary>
    // SHORTCUT: a character forgets where it walked, and how hurt it was, when it streams out, and comes back whole
    // where it was placed; its body takes the player's profile's shape (a race's record will give its own).
    public sealed class WorldCharacters : IDisposable
    {
        readonly WorldView _view;
        readonly GameStage _stage;
        readonly BodyShape _humanoid;
        readonly WanderSettings _settings;
        readonly Dictionary<RecordId, Character> _characters = new Dictionary<RecordId, Character>();

        /// <param name="stage">The game the characters stand in (its world says what each placed object is).</param>
        /// <param name="humanoid">The body a character stands on, made as tall as it is.</param>
        public WorldCharacters(WorldView view, GameStage stage, BodyShape humanoid, WanderSettings settings)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _stage = stage ?? throw new ArgumentNullException(nameof(stage));
            _humanoid = humanoid;
            _settings = settings;
            _view.PlacedShown += OnShown;
            _view.PlacedRemoving += OnRemoving;
            foreach (var pair in _view.PlacedObjects)
                OnShown(pair.Key, pair.Value);
        }

        /// <summary>How many characters go about now.</summary>
        public int Count => _characters.Count;

        /// <summary>Moves every character on by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f))
                return;
            foreach (var character in _characters.Values)
                character.Tick(deltaTime);
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase); every character moves with it.</summary>
        public void Shift(Vector3 delta)
        {
            foreach (var character in _characters.Values)
                character.Actor.Shift(delta);
        }

        public void Dispose()
        {
            _view.PlacedShown -= OnShown;
            _view.PlacedRemoving -= OnRemoving;
            foreach (var character in _characters.Values)
                character.Dispose();
            _characters.Clear();
        }

        // A placed character takes to its body where it stands, facing as placed; the placed object hides meanwhile.
        void OnShown(RecordId id, GameObject placed)
        {
            var animation = HumanoidAnimation.On(placed);
            if (animation == null || !TryGetKind(id, out var kind, out var prefab))
                return;

            // As tall as its eyes are high in its idle, at its walk's and its run's own paces (so its feet keep to the
            // ground); its own dice, from its id, so it goes about the same way each time the game is played.
            var shape = HumanoidHead.TryFindEyeLevel(prefab, animation.Clip(HumanoidAnimation.IdleMove), out var eyes)
                ? _humanoid.ForEyes(eyes)
                : _humanoid;
            if (!(eyes > 0f))
                eyes = shape.Height - BodyShape.HeadAboveEyes;
            var pace = animation.GroundSpeedOf(HumanoidAnimation.WalkMove);
            var run = animation.GroundSpeedOf(HumanoidAnimation.RunMove);
            var settings = pace > 0f ? _settings.WithSpeed(pace) : _settings;
            var walking = run > settings.Speed
                ? new LocomotionSettings(walkSpeed: settings.Speed, runSpeed: run)
                : new LocomotionSettings(walkSpeed: settings.Speed);

            var root = placed.transform.parent;
            var home = placed.transform.localPosition;
            var dice = new Random(StableHash(id.ToString()));
            var day = new Wander(new System.Numerics.Vector2(home.x, home.z), settings, dice);
            // Who it is while the world plays: the actor its base object names, if any (placed for its look alone, it is no one).
            var who = kind.Actor.HasValue && _stage.World.TryGet(kind.Actor.Value, out var record) && record is ActorRecord actor ? actor : null;
            var mind = who != null
                ? new Fighter(day, FighterSettings.SwordAndBoard(walking.WalkSpeed, walking.RunSpeed), dice)
                : (IMind)day;
            var forward = placed.transform.forward;
            _characters[id] = new Character(_stage, PlacedActors.IdOf(id), placed, root, kind, who, prefab, shape, walking, eyes, mind,
                Compass.HeadingOf(new System.Numerics.Vector2(forward.x, forward.z)));
        }

        void OnRemoving(RecordId id, GameObject placed)
        {
            if (!_characters.TryGetValue(id, out var character))
                return;
            character.Dispose();
            _characters.Remove(id);
        }

        // What a placed object is (its base object) and the prefab its look is.
        bool TryGetKind(RecordId id, out BaseObjectRecord kind, out GameObject prefab)
        {
            kind = null;
            prefab = null;
            if (!_stage.World.TryGet(id, out var record) || !(record is PlacedObjectRecord placed) ||
                !_stage.World.TryGet(placed.BaseId, out var baseRecord) || !(baseRecord is BaseObjectRecord baseObject))
                return false;
            kind = baseObject;
            return _view.Assets.TryGetTemplate(baseObject.AssetKey, out prefab);
        }

        // string.GetHashCode may differ between runs; this doesn't.
        static int StableHash(string text)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var c in text)
                    hash = (hash ^ c) * 16777619;
                return hash;
            }
        }

        // One character going about: the actor it is, and the mind telling it what to do.
        sealed class Character : IDisposable
        {
            readonly GameObject _placed;
            readonly Transform _root;
            readonly ActorEquipment _equipment;
            readonly IDisposable _attributeEvents;
            readonly IMind _mind;
            readonly Gaze _gaze;
            readonly List<GazeInterest> _interests = new List<GazeInterest>();

            /// <param name="kind">What it is placed as (its look, its name when it is no one).</param>
            /// <param name="record">Who it is: its name and what it starts with; null for a character that is no one (nothing hurts it, it holds nothing).</param>
            public Character(GameStage stage, ActorId id, GameObject placed, Transform root, BaseObjectRecord kind, ActorRecord record,
                GameObject prefab, BodyShape shape, LocomotionSettings walking, float eyes, IMind mind, float heading)
            {
                _placed = placed;
                _root = root;
                _mind = mind;
                _gaze = new Gaze(GazeSettings.Person, stage.Sight);
                // Its own numbers over the world's attributes, and its own pack and hands with what its record gives it
                // (a sword and a shield, put away), before the look is built, so they show on it from the start.
                var attributes = record != null && stage.Attributes != null ? new ActorAttributes(stage.Attributes, id) : null;
                if (attributes != null)
                    _attributeEvents = AttributeEvents.Bind(attributes, stage.Events);
                var inventory = new Inventory();
                _equipment = new ActorEquipment(id, inventory, stage.Events);
                if (record != null)
                    StartingItems.Give(record, stage.World, inventory, _equipment);
                Actor = new PlayedActor(stage, id, placed.name, shape, walking, eyes, attributes, inventory, _equipment,
                    record != null ? record.DisplayName : kind.DisplayName);
                Actor.Rules.Facing = BodyFacing.Movement;
                Actor.ChangeLook(prefab);
                Actor.PlaceAt(placed.transform.position, heading);
                _placed.SetActive(false);
            }

            public PlayedActor Actor { get; }

            // It sees, decides and does, each frame. The mind works where the world's things are placed (the world
            // root's space), so a rebase moves nothing of it; the actor, where its body is (the scene's).
            public void Tick(float deltaTime)
            {
                // Fallen, it lies where it fell: its mind has nothing to say, and it looks at no one.
                if (Actor.Fallen)
                {
                    _gaze.LookAway();
                    Actor.LooksAt = default;
                    Actor.Tick(ActorCommands.None, deltaTime);
                    return;
                }

                var rules = Actor.Rules;
                var decision = _mind.Decide(See(), deltaTime);
                var way = ToScene(new Vector3(decision.Velocity.X, 0f, decision.Velocity.Y));
                var speed = way.magnitude;
                var moving = speed > 1e-4f;

                // It faces as the mind says (squaring up to a foe, at the turn rate), else the way it goes.
                if (decision.Facing is float facing)
                {
                    var heading = SceneHeading(facing);
                    rules.Look.Set(heading, 0f);
                    rules.TargetHeading = heading;
                    rules.Facing = BodyFacing.Target;
                }
                else
                {
                    if (moving)
                        rules.Look.Set(Compass.HeadingOf(new System.Numerics.Vector2(way.x, way.z)), 0f);
                    rules.Facing = BodyFacing.Movement;
                }

                var move = PlayerCommands.None;
                if (moving)
                {
                    // The move in the look's frame (x right, y ahead), at the share of its walk, or of its run past it.
                    var paces = rules.Locomotion.Settings;
                    var walk = speed <= paces.WalkSpeed + 1e-3f;
                    var share = Math.Min(1f, speed / (walk ? paces.WalkSpeed : paces.RunSpeed)) / speed;
                    var ground = new System.Numerics.Vector2(way.x, way.z);
                    var ahead = System.Numerics.Vector2.Dot(ground, Compass.Forward(rules.Look.Heading)) * share;
                    var right = System.Numerics.Vector2.Dot(ground, Compass.Right(rules.Look.Heading)) * share;
                    move = new PlayerCommands(new System.Numerics.Vector2(right, ahead), System.Numerics.Vector2.Zero, sprint: false, jump: false, walk: walk);
                }

                Actor.Tick(new ActorCommands(move, decision.ReadyWeapon, decision.UseRight, decision.UseLeft, releaseLeft: decision.ReleaseLeft), deltaTime);

                // Its eyes go where its body now faces, its head after them.
                _gaze.Look(Actor.Eyes.ToNumerics(), rules.BodyHeading, Interests(), deltaTime);
                Actor.LooksAt = _gaze.Target;
            }

            // Whom it takes an interest in now, most first: the foe it fights, its eyes on its every move; then the player
            // passing by, if that isn't its foe.
            IReadOnlyList<GazeInterest> Interests()
            {
                _interests.Clear();
                var stage = Actor.Stage;
                if (Opponents.TryNearest(stage.Modes, Actor.Id, Actor.Rules.Feet, stage.Actors, out var foe, out _) &&
                    stage.Actors.TryGet(foe, out var fighting))
                    _interests.Add(new GazeInterest(foe, fighting.Eyes.ToNumerics(), focused: true));
                if (foe != ActorId.Player && stage.Actors.TryGet(ActorId.Player, out var player) && !player.Fallen)
                    _interests.Add(new GazeInterest(player.Id, player.Eyes.ToNumerics()));
                return _interests;
            }

            public void Dispose()
            {
                Actor.Dispose();
                _equipment.Dispose();
                _attributeEvents?.Dispose();
                if (_placed != null)
                    _placed.SetActive(true);
            }

            // What the mind sees now: where it stands, what it is up to, its nearest opponent standing in the game,
            // its weapons, and its hands.
            MindSight See()
            {
                var stage = Actor.Stage;
                var feet = Actor.Rules.Feet.ToUnity();
                var mode = stage.Modes.ModeOf(Actor.Id);
                System.Numerics.Vector2? opponent = null;
                if (Opponents.TryNearest(stage.Modes, Actor.Id, Actor.Rules.Feet, stage.Actors, out _, out var at))
                    opponent = Ground(at.ToUnity());

                var rightBusy = false;
                var guardUp = false;
                foreach (var swing in Actor.Combat.Swings)
                {
                    if (swing.Guards)
                        guardUp = true;
                    else if (swing.Slot == CoreEquipSlots.RightHand)
                        rightBusy = true;
                }

                return new MindSight(Ground(feet), mode, opponent, Actor.Readiness.State, rightBusy, guardUp);
            }

            // The ground plane (x, z) of the world root's space, where the mind works, from a scene position.
            System.Numerics.Vector2 Ground(Vector3 scene)
            {
                var at = _root != null ? _root.InverseTransformPoint(scene) : scene;
                return new System.Numerics.Vector2(at.x, at.z);
            }

            Vector3 ToScene(Vector3 direction) => _root != null ? _root.TransformDirection(direction) : direction;

            // A heading in the world root's space, as the scene has it.
            float SceneHeading(float heading)
            {
                var forward = Compass.Forward(heading);
                var scene = ToScene(new Vector3(forward.X, 0f, forward.Y));
                return Compass.HeadingOf(new System.Numerics.Vector2(scene.x, scene.z));
            }
        }
    }
}
