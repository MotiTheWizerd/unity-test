using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.World;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The world's loose things as physical bodies while it plays (<see cref="PhysicalObjects"/>: every item, and what
    /// has a <see cref="PhysicsPart"/>): each placed one the view shows gets a rigid body of its mass, asleep where it was
    /// placed until something touches it, as Skyrim's clutter waits. Whenever one has come to rest somewhere else, or
    /// streams out while away from where it was, <c>settled</c> hears its new pose, so the game keeps it there
    /// (<see cref="WorldChanges.Move"/>); one that falls out of the world is put back where it last rested. Things without
    /// a collider stay as they are. Bound for one game: disposed, every
    /// body goes and every object is back where its record has it. Scene coordinates.
    /// </summary>
    public sealed class WorldPhysics : IDisposable
    {
        // How far a body must be from where it last rested for its new rest to count, meters and degrees.
        const float MovedDistance = 0.01f;
        const float MovedAngle = 1f;

        // How far below where it last rested a body may fall before it is taken to have left the world (off its edge,
        // through a gap) and is put back there, meters.
        const float LostBelow = 100f;

        readonly WorldView _view;
        readonly IRecordStore _world;
        readonly Action<RecordId, Vector3, Quaternion> _settled;
        readonly Dictionary<RecordId, Body> _bodies = new Dictionary<RecordId, Body>();
        readonly List<RecordId> _ids = new List<RecordId>();

        /// <param name="world">The world as played, which says what each placed object is.</param>
        /// <param name="settled">Hears a placed object come to rest somewhere new, with its pose there (scene space).</param>
        public WorldPhysics(WorldView view, IRecordStore world, Action<RecordId, Vector3, Quaternion> settled)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _settled = settled ?? throw new ArgumentNullException(nameof(settled));
            _view.PlacedShown += OnShown;
            _view.PlacedRemoving += OnRemoving;
            foreach (var pair in _view.PlacedObjects)
                OnShown(pair.Key, pair.Value);
        }

        /// <summary>How many of the shown objects are physical bodies now.</summary>
        public int Count => _bodies.Count;

        /// <summary>Tells of every body that has come to rest somewhere new since last asked.</summary>
        public void Tick()
        {
            _ids.Clear();
            _ids.AddRange(_bodies.Keys);
            foreach (var id in _ids)
            {
                var body = _bodies[id];
                if (body.Rigidbody == null)
                    _bodies.Remove(id);
                else if (body.Rigidbody.IsSleeping())
                    TellIfMoved(id, body);
                else if (body.Rigidbody.position.y < body.RestPosition.y - LostBelow)
                    PutBack(body);
            }
        }

        // Fallen out of the world: back where it last rested, still.
        static void PutBack(Body body)
        {
            var rigidbody = body.Rigidbody;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.transform.SetPositionAndRotation(body.RestPosition, body.RestRotation);
            rigidbody.Sleep();
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase): where each body rested moves with it, so nothing seems moved.</summary>
        public void Shift(Vector3 delta)
        {
            foreach (var body in _bodies.Values)
                body.RestPosition += delta;
        }

        public void Dispose()
        {
            _view.PlacedShown -= OnShown;
            _view.PlacedRemoving -= OnRemoving;
            foreach (var body in _bodies.Values)
            {
                if (body.Rigidbody != null)
                    UnityEngine.Object.Destroy(body.Rigidbody);
            }

            _view.ResetTransforms(_bodies.Keys);
            _bodies.Clear();
        }

        void OnShown(RecordId id, GameObject go)
        {
            if (!_world.TryGet(id, out var record) || !(record is PlacedObjectRecord placed) ||
                !_world.TryGet(placed.BaseId, out var kind) || !(kind is BaseObjectRecord baseObject) ||
                !PhysicalObjects.TryGetMass(baseObject, out var mass) || !TryMakeSolid(go))
                return;

            if (!go.TryGetComponent(out Rigidbody rigidbody))
                rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.mass = mass;
            // Swords and bows are a few centimeters thick: a step's fall carries them through the ground unless each step
            // is swept (speculative contacts let them through the terrain). Asleep, it costs nothing.
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Asleep where it was placed: it waits to be touched, not to settle the moment it streams in.
            rigidbody.Sleep();
            var transform = go.transform;
            _bodies[id] = new Body(rigidbody, transform.position, transform.rotation);
        }

        // Streaming out (or taken) while away from where it last rested: where it is now is kept.
        void OnRemoving(RecordId id, GameObject go)
        {
            if (!_bodies.TryGetValue(id, out var body))
                return;
            _bodies.Remove(id);
            if (body.Rigidbody != null)
                TellIfMoved(id, body);
        }

        void TellIfMoved(RecordId id, Body body)
        {
            var transform = body.Rigidbody.transform;
            if (Vector3.Distance(transform.position, body.RestPosition) < MovedDistance &&
                Quaternion.Angle(transform.rotation, body.RestRotation) < MovedAngle)
                return;
            body.RestPosition = transform.position;
            body.RestRotation = transform.rotation;
            _settled(id, transform.position, transform.rotation);
        }

        // A body needs a solid shape to collide with: a mesh collider is made convex (PhysX moves no other). Without
        // any collider the object can't be a body.
        static bool TryMakeSolid(GameObject go)
        {
            var colliders = go.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0)
                return false;
            foreach (var collider in colliders)
            {
                if (collider is MeshCollider mesh && !mesh.convex)
                    mesh.convex = true;
            }

            return true;
        }

        // A physical object: its body, and where it last rested.
        sealed class Body
        {
            public Body(Rigidbody rigidbody, Vector3 position, Quaternion rotation)
            {
                Rigidbody = rigidbody;
                RestPosition = position;
                RestRotation = rotation;
            }

            public Rigidbody Rigidbody { get; }

            public Vector3 RestPosition { get; set; }

            public Quaternion RestRotation { get; set; }
        }
    }
}
