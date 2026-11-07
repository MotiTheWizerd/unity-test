using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Equipment;
using Forge.Game.Events;

namespace Forge.Game.Combat
{
    /// <summary>What came of asking an actor to attack.</summary>
    public enum AttackOutcome
    {
        /// <summary>The swing began.</summary>
        Attacking,

        /// <summary>Its weapons aren't drawn (or are still coming out).</summary>
        NotDrawn,

        /// <summary>The slot holds no weapon.</summary>
        NoWeapon,

        /// <summary>The weapon's style has no such move (a shield doesn't attack), or the world has no such style.</summary>
        NoMove,

        /// <summary>That hand is still swinging.</summary>
        Busy,

        /// <summary>Not stamina enough for the move.</summary>
        Tired,
    }

    /// <summary>
    /// How one actor fights, the player's or anyone's: each hand swings on its own (<see cref="Swings"/>), so two
    /// one-handers swing apart and a two-hander, taking both hands, swings once. A swing is the move its weapon's style
    /// has for the ask (<see cref="WeaponPart.Style"/>, <see cref="CombatStyleRecord"/>), timed as the look plays it
    /// (<see cref="Timings"/>), and only with weapons drawn (<see cref="WeaponReadiness"/>). While a swing is in its hit
    /// window, whatever finds what the weapon passes through (the look's blade, later a projectile) calls
    /// <see cref="Strike"/>: the weapon's blow (<see cref="WeaponPart.Hit"/>) is applied to the target's attributes, once
    /// per swing and target. Putting the weapons away, or taking the swinging one off, stops the swing. Told as it happens (<see cref="SwingStarted"/>, <see cref="SwingEnded"/>) for the look, and on
    /// the stream (<see cref="AttackStarted"/>, <see cref="HitLanded"/>, <see cref="ActorKilled"/>) for the rest.
    /// Engine-free; the game ticks it.
    /// </summary>
    public sealed class ActorCombat : IDisposable
    {
        readonly ActorEquipment _equipment;
        readonly WeaponReadiness _readiness;
        readonly IRecordStore _records;
        readonly ActorAttributes _attributes;
        readonly Combatants _combatants;
        readonly GameEvents _events;
        readonly List<Swing> _swings = new List<Swing>();

        /// <param name="records">The world, which weapons' styles are read from.</param>
        /// <param name="attributes">The actor's own, which moves spend stamina from; null for an actor without (nothing is spent).</param>
        /// <param name="combatants">Who can be struck.</param>
        /// <param name="events">The game's stream; null to tell no one.</param>
        public ActorCombat(ActorEquipment equipment, WeaponReadiness readiness, IRecordStore records, ActorAttributes attributes,
            Combatants combatants, GameEvents events = null)
        {
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
            _records = records ?? throw new ArgumentNullException(nameof(records));
            _attributes = attributes;
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _events = events;
            _readiness.Changed += OnReadinessChanged;
            _equipment.Unequipped += OnUnequipped;
        }

        public ActorId Owner => _equipment.Owner;

        /// <summary>The look's timings of the moves; until set, every move is timed <see cref="MoveTiming.Default"/>.</summary>
        public IMoveTimings Timings { get; set; }

        /// <summary>What is swinging now, a hand each.</summary>
        public IReadOnlyList<Swing> Swings => _swings;

        /// <summary>A swing began, for the look.</summary>
        public event Action<Swing> SwingStarted;

        /// <summary>A swing played out, for the look.</summary>
        public event Action<Swing> SwingEnded;

        /// <summary>Attacks with what <paramref name="slot"/> holds (the right hand's sword).</summary>
        public AttackOutcome TryAttack(RecordId slot) => TryMove(slot, CombatMoveKind.Attack);

        /// <summary>Lets <paramref name="deltaTime"/> seconds pass: swings come through their windows to their ends.</summary>
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return;
            for (var i = _swings.Count - 1; i >= 0; i--)
            {
                var swing = _swings[i];
                swing.Advance(deltaTime);
                if (!swing.IsOver)
                    continue;
                _swings.RemoveAt(i);
                SwingEnded?.Invoke(swing);
            }
        }

        /// <summary>
        /// <paramref name="swing"/>'s weapon passed through <paramref name="target"/>: its blow lands, if the swing is in
        /// its hit window, the target can be struck and isn't the swinger, and this swing hasn't struck it yet. Whether it landed.
        /// </summary>
        public bool Strike(Swing swing, ActorId target)
        {
            if (swing == null)
                throw new ArgumentNullException(nameof(swing));
            if (!_swings.Contains(swing) || !swing.IsStriking || target == Owner)
                return false;
            if (!_combatants.TryGet(target, out var attributes) || IsDead(attributes) || !swing.MarkStruck(target))
                return false;

            var weapon = swing.Weapon.Record;
            var health = 0f;
            foreach (var effect in weapon.Weapon.Hit)
            {
                var applied = AttributeEffects.Apply(effect, attributes);
                if (effect.Kind == EffectKind.Damage && effect.Attribute == CoreAttributes.Health)
                    health += applied;
            }

            _events?.Publish(new HitLanded(Owner, target, weapon.Id, health));
            if (IsDead(attributes))
                _events?.Publish(new ActorKilled(target, Owner));
            return true;
        }

        /// <summary>Stops every swing at once (the actor is taken away, its weapons put away).</summary>
        public void Stop()
        {
            for (var i = _swings.Count - 1; i >= 0; i--)
            {
                var swing = _swings[i];
                _swings.RemoveAt(i);
                SwingEnded?.Invoke(swing);
            }
        }

        public void Dispose()
        {
            _readiness.Changed -= OnReadinessChanged;
            _equipment.Unequipped -= OnUnequipped;
        }

        AttackOutcome TryMove(RecordId slot, CombatMoveKind kind)
        {
            if (_readiness.State != Readiness.Drawn)
                return AttackOutcome.NotDrawn;
            var equipped = _equipment.In(slot);
            var weapon = equipped?.Record.Weapon;
            if (weapon == null)
                return AttackOutcome.NoWeapon;
            if (!_records.TryGet(weapon.Style, out var record) || !(record is CombatStyleRecord style) || !style.TryGetMove(kind, out var move))
                return AttackOutcome.NoMove;
            foreach (var swinging in _swings)
            {
                if (Overlaps(swinging.Weapon.Fit, equipped.Fit))
                    return AttackOutcome.Busy;
            }

            if (move.StaminaCost > 0f && _attributes != null && _attributes.Set.Contains(CoreAttributes.Stamina) &&
                !_attributes.TrySpend(CoreAttributes.Stamina, move.StaminaCost))
                return AttackOutcome.Tired;

            var timing = Timings != null && Timings.TryGet(move.Animation, out var timed) ? timed : MoveTiming.Default;
            var swing = new Swing(slot, equipped, move, timing);
            _swings.Add(swing);
            SwingStarted?.Invoke(swing);
            _events?.Publish(new AttackStarted(Owner, slot, equipped.Item, kind));
            return AttackOutcome.Attacking;
        }

        static bool IsDead(ActorAttributes attributes) =>
            attributes.Set.Contains(CoreAttributes.Health) &&
            attributes.Current(CoreAttributes.Health) <= attributes.Set.Records[attributes.Set.SlotOf(CoreAttributes.Health)].Min;

        // A weapon put away or taken off stops swinging.
        void OnReadinessChanged(Readiness state)
        {
            if (state != Readiness.Drawn)
                Stop();
        }

        void OnUnequipped(EquippedItem equipped, UnequipReason reason)
        {
            for (var i = _swings.Count - 1; i >= 0; i--)
            {
                var swing = _swings[i];
                if (swing.Weapon != equipped)
                    continue;
                _swings.RemoveAt(i);
                SwingEnded?.Invoke(swing);
            }
        }

        static bool Overlaps(EquipFit a, EquipFit b)
        {
            foreach (var slot in a.Slots)
            {
                if (b.Takes(slot))
                    return true;
            }

            return false;
        }
    }
}
