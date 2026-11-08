using System;
using System.Collections.Generic;
using Forge.Game.Actors;
using Forge.Game.Cameras;
using Forge.Game.Combat;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// What an actor's bow looses, flown through the engine's world: each shot leaves the string where the arrow lay
    /// (<see cref="ShotView.TryDrawLine"/>), towards what the aim's line meets (the centre of the view, as Skyrim aims),
    /// at its speed (<see cref="Shot.Speed"/>), and falls as it flies (<see cref="Flight"/>). Every frame the way from
    /// where its point was to where it is is tested through the physics, so a fast arrow misses nothing between frames;
    /// what it meets, it sticks in, and whoever that is takes its blow (<see cref="ActorCombat.Strike(Shot, ActorId)"/>;
    /// whom a collider belongs to is <c>identify</c>'s to say). Stuck arrows stay a while, the newest
    /// <see cref="MostStuck"/> of them. Scene coordinates; bound while the look exists.
    /// </summary>
    public sealed class ArrowFlights : IDisposable
    {
        /// <summary>How many arrows stay stuck where they landed at most; the oldest goes first.</summary>
        public const int MostStuck = 40;

        // Seconds a stuck arrow stays, and the longest an arrow flies before it is given up on (gone out of the world).
        const float StuckSeconds = 60f;
        const float LongestFlight = 15f;

        // Meters an arrow's point sinks into what it strikes.
        const float Sinks = 0.08f;

        // Meters the aim's line is followed to find what it points at.
        const float AimReach = 1000f;

        readonly ActorCombat _combat;
        readonly CharacterModel _model;
        readonly IAssetCatalog _assets;
        readonly Func<ViewPose> _aim;
        readonly Func<Collider, ActorId?> _identify;
        readonly List<Flying> _flying = new List<Flying>();
        readonly Queue<(GameObject Look, float Until)> _stuck = new Queue<(GameObject, float)>();

        /// <param name="aim">Where the archer looks from and which way (the camera): what that line meets is aimed at.</param>
        /// <param name="identify">Whom a collider belongs to, or null for no one who can be struck.</param>
        public ArrowFlights(ActorCombat combat, CharacterModel model, IAssetCatalog assets, Func<ViewPose> aim, Func<Collider, ActorId?> identify)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _aim = aim ?? throw new ArgumentNullException(nameof(aim));
            _identify = identify ?? throw new ArgumentNullException(nameof(identify));
            _combat.Loosed += OnLoosed;
        }

        /// <summary>Flies every arrow in the air <paramref name="deltaTime"/> seconds on, and lets old stuck ones go.</summary>
        public void Tick(float deltaTime)
        {
            while (_stuck.Count > 0 && (_stuck.Peek().Look == null || _stuck.Peek().Until <= Time.time))
                Destroy(_stuck.Dequeue().Look);
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
                if (length > 1e-5f && Physics.Raycast(from, way / length, out var hit, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    _flying.RemoveAt(i);
                    Stick(arrow, hit, way / length);
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
            while (_stuck.Count > 0)
                Destroy(_stuck.Dequeue().Look);
        }

        // It leaves the string with its point where the arrow on it had its point, towards what the aim meets.
        void OnLoosed(Shot shot)
        {
            if (!ShotView.TryDrawLine(_model, out var tail, out var along))
                return;
            var look = _assets.Instantiate(shot.Ammo.AssetKey, null);
            if (look == null)
                return;
            foreach (var collider in look.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.Destroy(collider);

            var scale = look.transform.lossyScale.y;
            var arrow = new Flying { Shot = shot, Look = look, Point = ArrowLook.Point(look) * scale };
            var point = tail + along * ((ArrowLook.Tail(look) + ArrowLook.Point(look)) * scale);
            arrow.Flight = Flight.Toward(point.ToNumerics(), AimedAt(point).ToNumerics(), shot.Speed);
            _flying.Add(arrow);
            Pose(arrow, point, arrow.Flight.Heading.ToUnity());
        }

        // What the line of the aim meets first, else as far along it as it is followed; never nearer than the bow.
        Vector3 AimedAt(Vector3 from)
        {
            var view = _aim();
            var origin = view.Position.ToUnity();
            var forward = view.Forward.ToUnity();
            var target = Physics.Raycast(origin, forward, out var hit, AimReach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point
                : origin + forward * AimReach;
            // Something between the camera and the bow (a wall at the shoulder) is no aim: shoot along the view instead.
            return Vector3.Dot(target - from, forward) > 0f ? target : from + forward * AimReach;
        }

        void Stick(Flying arrow, RaycastHit hit, Vector3 heading)
        {
            var target = _identify(hit.collider);
            if (target.HasValue)
                _combat.Strike(arrow.Shot, target.Value);
            Pose(arrow, hit.point + heading * Sinks, heading);
            arrow.Look.transform.SetParent(hit.collider.transform, true);
            _stuck.Enqueue((arrow.Look, Time.time + StuckSeconds));
            while (_stuck.Count > MostStuck)
                Destroy(_stuck.Dequeue().Look);
        }

        // Its point at `point`, pointing along `heading`.
        static void Pose(Flying arrow, Vector3 point, Vector3 heading)
        {
            var rotation = Quaternion.FromToRotation(Vector3.up, heading);
            arrow.Look.transform.SetPositionAndRotation(point - rotation * Vector3.up * arrow.Point, rotation);
        }

        static void Destroy(GameObject look)
        {
            if (look != null)
                UnityEngine.Object.Destroy(look);
        }

        // An arrow in the air: what was shot, its look, how far its point lies ahead of its pivot (meters), where it is.
        sealed class Flying
        {
            public Shot Shot;
            public GameObject Look;
            public float Point;
            public Flight Flight;
            public float Age;
        }
    }
}
