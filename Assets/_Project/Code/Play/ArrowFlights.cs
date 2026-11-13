using System;
using System.Collections.Generic;
using Forge.Game.Actors;
using Forge.Game.Cameras;
using Forge.Game.Combat;
using Forge.Game.World;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// What an actor's bow looses, flown through the engine's world: each shot leaves the string where the arrow lay
    /// (<see cref="ArcheryAim.TryDrawLine"/>), towards what the aim's line meets (<see cref="ArcheryAim.Target"/>),
    /// at its speed (<see cref="Shot.Speed"/>), and falls as it flies (<see cref="Flight"/>). Every frame the way from
    /// where its point was to where it is is tested through the physics (the world's things, and the actors where they
    /// are drawn: their hitboxes and raised guards; but the archer's own, which it leaves from beside:
    /// <see cref="ArcheryAim.TryMeet"/>), so a fast arrow misses nothing between frames; what it meets, it sticks in, and whoever that
    /// is takes its blow, on the part it met (<see cref="ActorCombat.Strike(Shot, ActorId, System.Numerics.Vector3?, BlowMet, BodyPart)"/>;
    /// whom a collider belongs to is <c>identify</c>'s to say). Stuck in someone (a hitbox, a shield's face) it stays in
    /// them as it struck, moving with them, a while (<see cref="LodgedSeconds"/>), and is handed to <c>lodged</c> (the
    /// game gives it to whom it is in, or it breaks: <see cref="ShotLandings"/>). Stuck in the world, a loose thing it
    /// meets is pushed by it, its whole momentum given at the point struck; it is handed to <c>landed</c> with its pose
    /// there and the loose thing it is in, if any (the game keeps it as the item it is, or it breaks), and its flying look
    /// goes. Scene coordinates; bound while the look exists.
    /// </summary>
    public sealed class ArrowFlights : IDisposable
    {
        /// <summary>
        /// A shot stuck at <paramref name="position"/>, turned <paramref name="rotation"/> (its look's pose, scene space),
        /// in <paramref name="stuckIn"/>, the loose thing it struck; null when that doesn't move (the ground, a wall).
        /// </summary>
        public delegate void Landing(Shot shot, Vector3 position, Quaternion rotation, Rigidbody stuckIn);

        /// <summary>A shot stuck in <paramref name="into"/> (its body, or the shield it holds up).</summary>
        public delegate void Lodging(Shot shot, ActorId into);

        /// <summary>Seconds an arrow stays seen in whom it struck before it goes (what it is, they carry).</summary>
        public const float LodgedSeconds = 30f;

        // The longest an arrow flies before it is given up on (gone out of the world), seconds.
        const float LongestFlight = 15f;

        // Meters an arrow's point sinks into what it strikes.
        const float Sinks = 0.08f;

        readonly ActorCombat _combat;
        readonly CharacterModel _model;
        readonly IAssetCatalog _assets;
        readonly Func<ViewPose> _aim;
        readonly Func<Collider, ActorId?> _identify;
        readonly Landing _landed;
        readonly Lodging _lodged;
        readonly List<Flying> _flying = new List<Flying>();

        /// <param name="aim">Where the archer looks from and which way (the camera): what that line meets is aimed at.</param>
        /// <param name="identify">Whom a collider belongs to, or null for no one who can be struck.</param>
        /// <param name="landed">Where a shot that stuck in the world goes.</param>
        /// <param name="lodged">Where a shot that stuck in someone goes.</param>
        public ArrowFlights(ActorCombat combat, CharacterModel model, IAssetCatalog assets, Func<ViewPose> aim, Func<Collider, ActorId?> identify,
            Landing landed, Lodging lodged)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _aim = aim ?? throw new ArgumentNullException(nameof(aim));
            _identify = identify ?? throw new ArgumentNullException(nameof(identify));
            _landed = landed ?? throw new ArgumentNullException(nameof(landed));
            _lodged = lodged ?? throw new ArgumentNullException(nameof(lodged));
            _combat.Loosed += OnLoosed;
        }

        /// <summary>Flies every arrow in the air <paramref name="deltaTime"/> seconds on.</summary>
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f))
                return;

            for (var i = _flying.Count - 1; i >= 0; i--)
            {
                var arrow = _flying[i];
                var next = arrow.Flight.Step(deltaTime);
                arrow.Age += deltaTime;
                var from = arrow.Flight.Position.ToUnity();
                var way = next.Position.ToUnity() - from;
                var length = way.magnitude;
                if (length > 1e-5f && TryMeet(from, way / length, length, out var hit))
                {
                    _flying.RemoveAt(i);
                    Stick(arrow, hit, way / length, next.Velocity.ToUnity());
                    continue;
                }

                arrow.Flight = next;
                if (arrow.Age > LongestFlight)
                {
                    _flying.RemoveAt(i);
                    Destroy(arrow.Look);
                    continue;
                }

                Pose(arrow, next.Position.ToUnity(), next.Heading.ToUnity());
            }
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase): what is in the air moves with it.</summary>
        public void Shift(Vector3 delta)
        {
            foreach (var arrow in _flying)
                arrow.Flight = new Flight(arrow.Flight.Position + delta.ToNumerics(), arrow.Flight.Velocity);
        }

        public void Dispose()
        {
            _combat.Loosed -= OnLoosed;
            foreach (var arrow in _flying)
                Destroy(arrow.Look);
            _flying.Clear();
        }

        // It leaves the string with its point where the arrow on it had its point, towards what the aim meets.
        void OnLoosed(Shot shot)
        {
            if (!ArcheryAim.TryDrawLine(_model, out var tail, out var along))
                return;
            var look = _assets.Instantiate(shot.Ammo.AssetKey, null);
            if (look == null)
                return;
            foreach (var collider in look.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.Destroy(collider);

            var point = tail + along * (ArrowLook.Tail(look) + ArrowLook.Point(look));
            var arrow = new Flying { Shot = shot, Look = look };
            arrow.Flight = Flight.Toward(point.ToNumerics(), ArcheryAim.Target(_aim(), point, _model).ToNumerics(), shot.Speed);
            _flying.Add(arrow);
            Pose(arrow, point, arrow.Flight.Heading.ToUnity());
        }

        // The nearest thing on the arrow's way, never the shooter's own, which it leaves from beside and must never stick in.
        bool TryMeet(Vector3 from, Vector3 direction, float length, out RaycastHit nearest) =>
            ArcheryAim.TryMeet(from, direction, length, _model, out nearest);

        void Stick(Flying arrow, RaycastHit hit, Vector3 heading, Vector3 velocity)
        {
            var target = _identify(hit.collider);
            if (target.HasValue)
            {
                var guard = ArcheryAim.IsGuard(hit.collider);
                _combat.Strike(arrow.Shot, target.Value, heading.ToNumerics(), guard ? BlowMet.Guard : BlowMet.Body,
                    guard ? BodyPart.None : Hitbox.PartOf(hit.collider));
                if (ArcheryAim.IsOnSomeone(hit.collider))
                {
                    Lodge(arrow, hit, heading);
                    _lodged(arrow.Shot, target.Value);
                    return;
                }
            }

            // It stops in what it strikes, so all it carried goes into it (Skyrim knocks a cup off a shelf so).
            var struck = hit.rigidbody != null && !hit.rigidbody.isKinematic ? hit.rigidbody : null;
            if (struck != null && PhysicalObjects.TryGetMass(arrow.Shot.Ammo, out var mass))
                struck.AddForceAtPosition(velocity * mass, hit.point, ForceMode.Impulse);
            Pose(arrow, hit.point + heading * Sinks, heading);
            var look = arrow.Look.transform;
            _landed(arrow.Shot, look.position, look.rotation, struck);
            Destroy(arrow.Look);
        }

        // Its look stays in what it met, sunk in as it struck, and rides it: placed by where that stood in the physics when
        // it was met (a moving collider's body there may be a step behind its bone), then hung from it; gone in a while.
        void Lodge(Flying arrow, RaycastHit hit, Vector3 heading)
        {
            Pose(arrow, hit.point + heading * Sinks, heading);
            var look = arrow.Look.transform;
            var carrier = hit.collider.transform;
            var body = hit.collider.attachedRigidbody;
            if (body != null)
            {
                var back = Quaternion.Inverse(body.rotation);
                var offset = back * (look.position - body.position);
                var turn = back * look.rotation;
                look.SetPositionAndRotation(carrier.position + carrier.rotation * offset, carrier.rotation * turn);
            }

            look.SetParent(carrier, true);
            UnityEngine.Object.Destroy(arrow.Look, LodgedSeconds);
        }

        // Its point at `point`, pointing along `heading`.
        static void Pose(Flying arrow, Vector3 point, Vector3 heading) => ArrowLook.LayByPoint(arrow.Look, point, heading);

        static void Destroy(GameObject look)
        {
            if (look != null)
                UnityEngine.Object.Destroy(look);
        }

        // An arrow in the air: what was shot, its look, where it is, how long it has flown.
        sealed class Flying
        {
            public Shot Shot;
            public GameObject Look;
            public Flight Flight;
            public float Age;
        }
    }
}
