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
    /// placed until something touches it, as Skyrim's clutter waits, or moving when it is let go (<see cref="Release"/>:
    /// dropped). The light ones are clutter, on the <see cref="ClutterLayer"/>, which actors walk through and kick.
    /// Whenever one has come to rest somewhere else, or streams out while away from where it was, <c>settled</c> hears
    /// its new pose, so the game keeps it there (<see cref="WorldChanges.Move"/>); one that falls out of the world is put
    /// back where it last rested. Things without a collider stay as they are. Bound for one game: disposed, every body
    /// goes and every object is back where its record has it, on its own layers. Scene coordinates.
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
        readonly List<Rigidbody> _asleep = new List<Rigidbody>();

        // What is stuck in something (an arrow landed), by what it is stuck in: null for what doesn't move.
        readonly Dictionary<RecordId, RecordId?> _stuck = new Dictionary<RecordId, RecordId?>();
        readonly List<RecordId> _ties = new List<RecordId>();

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

        /// <summary>
        /// Lets the loose thing <paramref name="id"/> go moving (dropped from the hands): awake, at
        /// <paramref name="velocity"/>, turning at <paramref name="spin"/> (radians a second about each axis), to come to
        /// rest wherever it falls. False when it is no body (it isn't shown, or it has no collider).
        /// </summary>
        public bool Release(RecordId id, Vector3 velocity, Vector3 spin)
        {
            if (!_bodies.TryGetValue(id, out var body) || body.Rigidbody == null)
                return false;
            var rigidbody = body.Rigidbody;
            rigidbody.WakeUp();
            rigidbody.linearVelocity = velocity;
            rigidbody.angularVelocity = spin;
            return true;
        }

        /// <summary>
        /// The loose thing <paramref name="id"/> (an arrow landed) is stuck in <paramref name="host"/>: it rides along with
        /// it when that is a loose thing too, else (the ground, a wall, a dummy: null) it stays fixed where it is, never
        /// knocked over, until it is taken. Kept while the game lasts, so it is stuck again when it streams back in; when
        /// what it is in goes (taken), it falls.
        /// </summary>
        public void Stick(RecordId id, Rigidbody host)
        {
            var link = host != null ? host.GetComponent<RecordLink>() : null;
            _stuck[id] = link != null && _bodies.ContainsKey(link.RecordId) ? link.RecordId : (RecordId?)null;
            if (_bodies.TryGetValue(id, out var body))
                HoldStuck(id, body);
        }

        // A stuck thing tied to the body it is in, when that is shown; else fixed where it is.
        void HoldStuck(RecordId id, Body body)
        {
            if (!_stuck.TryGetValue(id, out var host) || body.Rigidbody == null)
                return;
            var rigidbody = body.Rigidbody;
            if (host.HasValue && _bodies.TryGetValue(host.Value, out var into) && into.Rigidbody != null)
            {
                SetFixed(rigidbody, false);
                if (body.Joint == null)
                {
                    // Joined, the two never collide, though its point is sunk in.
                    body.Joint = rigidbody.gameObject.AddComponent<FixedJoint>();
                    body.Joint.connectedBody = into.Rigidbody;
                }
            }
            else
                SetFixed(rigidbody, true);
        }

        // What can't be pushed (kinematic) can't be swept either: it is tested by speculative contacts meanwhile.
        static void SetFixed(Rigidbody rigidbody, bool isFixed)
        {
            rigidbody.collisionDetectionMode = isFixed ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
            rigidbody.isKinematic = isFixed;
        }

        // What was stuck in a body that goes (taken, streamed out) falls free of it.
        void Unstick(RecordId host)
        {
            foreach (var pair in _bodies)
            {
                var body = pair.Value;
                if (body.Joint == null || !_stuck.TryGetValue(pair.Key, out var into) || into != host)
                    continue;
                UnityEngine.Object.DestroyImmediate(body.Joint);
                body.Joint = null;
                _stuck.Remove(pair.Key);
                if (body.Rigidbody != null)
                    body.Rigidbody.WakeUp();
            }
        }

        /// <summary>
        /// The scene moved by <paramref name="delta"/> under the world (a rebase): where each body rested moves with it,
        /// so nothing seems moved, and what was asleep sleeps on.
        /// </summary>
        public void Shift(Vector3 delta)
        {
            _asleep.Clear();
            foreach (var body in _bodies.Values)
            {
                body.RestPosition += delta;
                if (body.Rigidbody != null && body.Rigidbody.IsSleeping())
                    _asleep.Add(body.Rigidbody);
            }

            // The root's move would reach the bodies at the next physics step and wake them all, and a pile woken settles
            // again a little apart (a cup creeps at every rebase): moved now, the sleeping are put back to sleep.
            Physics.SyncTransforms();
            foreach (var rigidbody in _asleep)
                rigidbody.Sleep();
            _asleep.Clear();
        }

        public void Dispose()
        {
            _view.PlacedShown -= OnShown;
            _view.PlacedRemoving -= OnRemoving;
            foreach (var body in _bodies.Values)
            {
                // A joint needs its body: it goes first, at once.
                if (body.Joint != null)
                    UnityEngine.Object.DestroyImmediate(body.Joint);
                if (body.Rigidbody != null)
                    UnityEngine.Object.Destroy(body.Rigidbody);
                body.RestoreLayers();
            }

            _view.ResetTransforms(_bodies.Keys);
            _bodies.Clear();
            _stuck.Clear();
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
            var body = new Body(rigidbody, transform.position, transform.rotation, PhysicalObjects.IsClutter(mass) ? ToClutter(go) : null);
            _bodies[id] = body;
            HoldStuck(id, body);
            // Shown again, what is stuck in it is tied to it again (shown while Tick tells of rests, so a list of its own).
            _ties.Clear();
            foreach (var pair in _stuck)
            {
                if (pair.Value == id && _bodies.ContainsKey(pair.Key))
                    _ties.Add(pair.Key);
            }

            foreach (var stuck in _ties)
                HoldStuck(stuck, _bodies[stuck]);
            _ties.Clear();
        }

        // Clutter's colliders go on the clutter layer while it plays; the layers they were on are kept to put them back.
        static (GameObject Part, int Layer)[] ToClutter(GameObject go)
        {
            var layer = ClutterLayer.Index;
            if (layer < 0)
                return null;
            var colliders = go.GetComponentsInChildren<Collider>();
            var was = new (GameObject, int)[colliders.Length];
            for (var i = 0; i < colliders.Length; i++)
            {
                var part = colliders[i].gameObject;
                was[i] = (part, part.layer);
                part.layer = layer;
            }

            return was;
        }

        // Streaming out (or taken) while away from where it last rested: where it is now is kept.
        void OnRemoving(RecordId id, GameObject go)
        {
            if (!_bodies.TryGetValue(id, out var body))
                return;
            _bodies.Remove(id);
            if (body.Rigidbody != null)
                TellIfMoved(id, body);
            Unstick(id);
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

        // A physical object: its body, where it last rested, and the layers its colliders were on, when it is clutter.
        sealed class Body
        {
            readonly (GameObject Part, int Layer)[] _layers;

            public Body(Rigidbody rigidbody, Vector3 position, Quaternion rotation, (GameObject Part, int Layer)[] layers)
            {
                Rigidbody = rigidbody;
                RestPosition = position;
                RestRotation = rotation;
                _layers = layers;
            }

            public Rigidbody Rigidbody { get; }

            public Vector3 RestPosition { get; set; }

            public Quaternion RestRotation { get; set; }

            /// <summary>What ties it to the body it is stuck in, if it is stuck in one that is shown.</summary>
            public FixedJoint Joint { get; set; }

            public void RestoreLayers()
            {
                if (_layers == null)
                    return;
                foreach (var (part, layer) in _layers)
                {
                    if (part != null)
                        part.layer = layer;
                }
            }
        }
    }
}
