using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
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
    /// while it plays, each as the player is: a body of its own (<see cref="ActorBody"/>, of the humanoid
    /// <see cref="BodyShape"/> at its own height), its look riding on it (<see cref="CharacterModel"/>) and the same
    /// rules moving it (<see cref="PlayerCharacter"/>: gravity, slopes, steps, turning the way it goes), told where to go
    /// by its mind (<see cref="Wander"/>: strolling about where it was placed) rather than by keys. Its body stands in
    /// the player's way and the player in its. While it goes about, the placed object it was stands hidden; bound for one
    /// game, disposed, every body goes and every placed object shows again where its record has it.
    /// </summary>
    // SHORTCUT: a character forgets where it walked when it streams out, and comes back home; its body takes the
    // player's profile's shape (a race's record will give its own); no blow or arrow reaches it yet (SceneLayers).
    public sealed class WorldCharacters : IDisposable
    {
        readonly WorldView _view;
        readonly IRecordStore _world;
        readonly BodyShape _humanoid;
        readonly WanderSettings _settings;
        readonly Dictionary<RecordId, Character> _characters = new Dictionary<RecordId, Character>();

        /// <param name="world">The world as played, which says what each placed object is.</param>
        /// <param name="humanoid">The body a character stands on, made as tall as it is.</param>
        public WorldCharacters(WorldView view, IRecordStore world, BodyShape humanoid, WanderSettings settings)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _world = world ?? throw new ArgumentNullException(nameof(world));
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
                character.Rules.Shift(delta.ToNumerics());
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
            if (animation == null || !TryGetLook(id, out var prefab))
                return;

            // As tall as its eyes are high in its idle, at its walk's own pace (so its feet keep to the ground); its own
            // dice, from its id, so it strolls the same way each time the game is played.
            var shape = HumanoidHead.TryFindEyeLevel(prefab, animation.Clip(HumanoidAnimation.IdleMove), out var eyes)
                ? _humanoid.ForEyes(eyes)
                : _humanoid;
            if (!(eyes > 0f))
                eyes = shape.Height - BodyShape.HeadAboveEyes;
            var pace = animation.GroundSpeedOf(HumanoidAnimation.WalkMove);
            var settings = pace > 0f ? _settings.WithSpeed(pace) : _settings;
            var walking = new LocomotionSettings(walkSpeed: settings.Speed);

            var root = placed.transform.parent;
            var home = placed.transform.localPosition;
            var mind = new Wander(new System.Numerics.Vector2(home.x, home.z), settings, new Random(StableHash(id.ToString())));
            var forward = placed.transform.forward;
            _characters[id] = new Character(placed, root, prefab, shape, walking, eyes, mind,
                Compass.HeadingOf(new System.Numerics.Vector2(forward.x, forward.z)));
        }

        void OnRemoving(RecordId id, GameObject placed)
        {
            if (!_characters.TryGetValue(id, out var character))
                return;
            character.Dispose();
            _characters.Remove(id);
        }

        // The prefab a placed object shows, by its base object's look.
        bool TryGetLook(RecordId id, out GameObject prefab)
        {
            prefab = null;
            return _world.TryGet(id, out var record) && record is PlacedObjectRecord placed &&
                _world.TryGet(placed.BaseId, out var kind) && kind is BaseObjectRecord baseObject &&
                _view.Assets.TryGetTemplate(baseObject.AssetKey, out prefab);
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

        // One character going about: its body, its look on it, the rules moving it and the mind telling them where.
        sealed class Character : IDisposable
        {
            readonly GameObject _placed;
            readonly Transform _root;
            readonly ActorBody _body;
            readonly CharacterModel _model;
            readonly Wander _mind;

            public Character(GameObject placed, Transform root, GameObject prefab, BodyShape shape, LocomotionSettings walking, float eyes,
                Wander mind, float heading)
            {
                _placed = placed;
                _root = root;
                _mind = mind;
                _body = new ActorBody(placed.name, shape);
                _model = new CharacterModel(_body.Body, prefab, _body.Hover);
                Rules = new PlayerCharacter(_body, walking, eyes) { Facing = BodyFacing.Movement };
                Rules.PlaceAt(placed.transform.position.ToNumerics(), heading);
                _model.Face(Rules.BodyHeading);
                _model.Animate(Rules.Motion, 0f);
                _placed.SetActive(false);
            }

            public PlayerCharacter Rules { get; }

            // The mind works where the world's things are placed (the world root's space), so a rebase moves nothing
            // of it; the rules, where the body is (the scene's).
            public void Tick(float deltaTime)
            {
                var feet = Rules.Feet.ToUnity();
                var at = _root != null ? _root.InverseTransformPoint(feet) : feet;
                var velocity = _mind.Step(new System.Numerics.Vector2(at.x, at.z), deltaTime);
                var way = new Vector3(velocity.X, 0f, velocity.Y);
                if (_root != null)
                    way = _root.TransformDirection(way);

                var commands = PlayerCommands.None;
                var speed = way.magnitude;
                if (speed > 1e-4f)
                {
                    // Straight ahead the way it is going, at the share of its walk the mind asks for.
                    Rules.Look.Set(Compass.HeadingOf(new System.Numerics.Vector2(way.x, way.z)), 0f);
                    var ahead = Math.Min(1f, speed / Rules.Locomotion.Settings.WalkSpeed);
                    commands = new PlayerCommands(new System.Numerics.Vector2(0f, ahead), System.Numerics.Vector2.Zero, sprint: false, jump: false,
                        walk: true);
                }

                Rules.Tick(commands, deltaTime);
                _model.Face(Rules.BodyHeading);
                _model.Animate(Rules.Motion, deltaTime);
            }

            public void Dispose()
            {
                _model.Dispose();
                _body.Dispose();
                if (_placed != null)
                    _placed.SetActive(true);
            }
        }
    }
}
