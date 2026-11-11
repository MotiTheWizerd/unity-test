using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Combat;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Finds what an actor's blade passes through while a swing is in its hit window, through the engine's physics: each
    /// frame, lines from where points along the blade were the frame before to where they are now, and the blade itself,
    /// so a fast swing misses nothing between frames, whatever the weapon's length (<see cref="CharacterModel.TryGetBlade"/>).
    /// The world's things and the actors' bodies alike are met (<see cref="SceneLayers.Targets"/>), and the raised guards'
    /// faces (<see cref="GuardFace"/>); along each line, nearest first, the blade stops at the first thing it can't cut
    /// through: a raised guard (the blow is the guard's: <see cref="BlowMet.Guard"/>, and the one behind it is spared), or
    /// something solid of the world's (a placed wall, a rock: <see cref="ActorCombat.Bounce"/>); the bodies it meets
    /// before that it cuts through (<see cref="BlowMet.Body"/>). The ground and loose things it passes. Whom each
    /// collider belongs to is <paramref name="identify"/>'s to say, and the rules decide the rest
    /// (<see cref="ActorCombat.Strike"/>: once per swing and target, never the swinger, whose own body and guard the blade
    /// passes), the blow coming from the swinger toward where it struck, for the target's guard. A block's swing sweeps
    /// nothing. Bound while the look exists.
    /// </summary>
    public sealed class BladeSweep : IDisposable
    {
        // Points along the blade traced from frame to frame: the grip, the tip and those between.
        const int Points = 6;

        static readonly IComparer<RaycastHit> Nearest = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        readonly ActorCombat _combat;
        readonly CharacterModel _model;
        readonly IRecordStore _records;
        readonly Func<Collider, ActorId?> _identify;
        readonly Dictionary<Swing, Vector3[]> _last = new Dictionary<Swing, Vector3[]>();
        readonly RaycastHit[] _hits = new RaycastHit[16];
        readonly List<Swing> _striking = new List<Swing>();

        // What this frame's lines met, for one swing: raised guards and bodies by whose (where first), and whether
        // something solid of the world's stopped it.
        readonly Dictionary<ActorId, Vector3> _guards = new Dictionary<ActorId, Vector3>();
        readonly Dictionary<ActorId, Vector3> _bodies = new Dictionary<ActorId, Vector3>();
        bool _wall;

        /// <param name="records">The world, which a swing's slot's bone is read from.</param>
        /// <param name="identify">Whom a collider belongs to, or null for no one who can be struck.</param>
        public BladeSweep(ActorCombat combat, CharacterModel model, IRecordStore records, Func<Collider, ActorId?> identify)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _records = records ?? throw new ArgumentNullException(nameof(records));
            _identify = identify ?? throw new ArgumentNullException(nameof(identify));
            _combat.SwingEnded += Forget;
        }

        /// <summary>Traces each swing in its hit window from last frame's blade to this frame's, and lets the rules have what it met.</summary>
        public void Tick()
        {
            // A blow may end a swing (it recoils): the swings are read before any is struck with.
            foreach (var swing in Striking())
            {
                if (!TryBone(swing.Slot, out var bone) || !_model.TryGetBlade(bone, out var from, out var to))
                {
                    _last.Remove(swing);
                    continue;
                }

                var now = new Vector3[Points];
                for (var i = 0; i < Points; i++)
                    now[i] = Vector3.Lerp(from, to, i / (Points - 1f));
                _guards.Clear();
                _bodies.Clear();
                _wall = false;
                Trace(from, to);
                if (_last.TryGetValue(swing, out var before))
                {
                    for (var i = 0; i < Points; i++)
                        Trace(before[i], now[i]);
                }

                _last[swing] = now;
                Resolve(swing);
            }
        }

        public void Dispose() => _combat.SwingEnded -= Forget;

        // The swings whose blades cut now (in their windows, not a block's).
        List<Swing> Striking()
        {
            _striking.Clear();
            foreach (var swing in _combat.Swings)
            {
                if (swing.IsStriking && !swing.Guards)
                    _striking.Add(swing);
                else
                    _last.Remove(swing);
            }

            return _striking;
        }

        // Along one line, nearest first: past its own body and guard, other triggers and what it passes (the ground,
        // loose things); a raised guard or something solid stops the line; a body is cut through.
        void Trace(Vector3 from, Vector3 to)
        {
            var way = to - from;
            var length = way.magnitude;
            if (length < 1e-4f)
                return;
            var count = Physics.RaycastNonAlloc(from, way / length, _hits, length, SceneLayers.Targets, QueryTriggerInteraction.Collide);
            Array.Sort(_hits, 0, count, Nearest);
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                var who = _identify(hit.collider);
                if (who == _combat.Owner)
                    continue;
                if (hit.collider.isTrigger)
                {
                    if (who.HasValue && hit.collider.GetComponent<GuardFace>() != null)
                    {
                        Note(_guards, who.Value, hit.point);
                        return;
                    }

                    continue;
                }

                if (who.HasValue)
                {
                    Note(_bodies, who.Value, hit.point);
                    continue;
                }

                if (IsSolid(hit.collider))
                {
                    _wall = true;
                    return;
                }
            }
        }

        // The rules have it: each raised guard met takes its blow (and spares the one behind it), each body met past
        // any guard takes its own, and a wall bounces what is left of the swing.
        void Resolve(Swing swing)
        {
            foreach (var guard in _guards)
                _combat.Strike(swing, guard.Key, From(guard.Value), BlowMet.Guard);
            foreach (var body in _bodies)
            {
                if (!_guards.ContainsKey(body.Key))
                    _combat.Strike(swing, body.Key, From(body.Value), BlowMet.Body);
            }

            if (_wall)
                _combat.Bounce(swing);
        }

        static void Note(Dictionary<ActorId, Vector3> met, ActorId who, Vector3 point)
        {
            if (!met.ContainsKey(who))
                met.Add(who, point);
        }

        // The blow's way, from the swinger toward where it struck.
        System.Numerics.Vector3 From(Vector3 point) => (point - _model.Root.transform.position).ToNumerics();

        // Something of the world's a blade can't cut through: a placed thing that stands (a wall, a rock, a tree), not the
        // ground (the terrain, though its chunks are records too), nor a loose thing that moves (a dropped sword), nor clutter.
        static bool IsSolid(Collider collider) =>
            !(collider is TerrainCollider) &&
            (SceneLayers.Solid & (1 << collider.gameObject.layer)) != 0 &&
            (collider.attachedRigidbody == null || collider.attachedRigidbody.isKinematic) &&
            collider.GetComponentInParent<RecordLink>() != null;

        bool TryBone(RecordId slot, out string bone)
        {
            bone = _records.TryGet(slot, out var record) && record is EquipSlotRecord equipSlot ? equipSlot.Bone : null;
            return bone != null;
        }

        void Forget(Swing swing) => _last.Remove(swing);
    }
}
