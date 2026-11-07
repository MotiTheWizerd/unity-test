using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Combat;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Finds what an actor's blade passes through while a swing is in its hit window, through the engine's physics: each
    /// frame, lines from where points along the blade were the frame before to where they are now, and the blade itself,
    /// so a fast swing misses nothing between frames, whatever the weapon's length (<see cref="CharacterModel.TryGetBlade"/>).
    /// Whom each collider belongs to is <paramref name="identify"/>'s to say; the rules decide the rest
    /// (<see cref="ActorCombat.Strike"/>: once per swing and target, never the swinger). Bound while the look exists.
    /// </summary>
    public sealed class BladeSweep : IDisposable
    {
        // Points along the blade traced from frame to frame: the grip, the tip and those between.
        const int Points = 6;

        readonly ActorCombat _combat;
        readonly CharacterModel _model;
        readonly IRecordStore _records;
        readonly Func<Collider, ActorId?> _identify;
        readonly Dictionary<Swing, Vector3[]> _last = new Dictionary<Swing, Vector3[]>();
        readonly RaycastHit[] _hits = new RaycastHit[16];

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

        /// <summary>Traces each swing in its hit window from last frame's blade to this frame's.</summary>
        public void Tick()
        {
            foreach (var swing in _combat.Swings)
            {
                if (!swing.IsStriking || !TryBone(swing.Slot, out var bone) || !_model.TryGetBlade(bone, out var from, out var to))
                {
                    _last.Remove(swing);
                    continue;
                }

                var now = new Vector3[Points];
                for (var i = 0; i < Points; i++)
                    now[i] = Vector3.Lerp(from, to, i / (Points - 1f));
                Trace(swing, from, to);
                if (_last.TryGetValue(swing, out var before))
                {
                    for (var i = 0; i < Points; i++)
                        Trace(swing, before[i], now[i]);
                }

                _last[swing] = now;
            }
        }

        public void Dispose() => _combat.SwingEnded -= Forget;

        void Trace(Swing swing, Vector3 from, Vector3 to)
        {
            var way = to - from;
            var length = way.magnitude;
            if (length < 1e-4f)
                return;
            var count = Physics.RaycastNonAlloc(from, way / length, _hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var target = _identify(_hits[i].collider);
                if (target.HasValue)
                    _combat.Strike(swing, target.Value);
            }
        }

        bool TryBone(RecordId slot, out string bone)
        {
            bone = _records.TryGet(slot, out var record) && record is EquipSlotRecord equipSlot ? equipSlot.Bone : null;
            return bone != null;
        }

        void Forget(Swing swing) => _last.Remove(swing);
    }
}
