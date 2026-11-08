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

        /// <summary>The weapon shoots, and nothing it shoots is equipped (a bow without arrows).</summary>
        NoAmmo,
    }

    /// <summary>
    /// How one actor fights, the player's or anyone's: each hand swings on its own (<see cref="Swings"/>), so two
    /// one-handers swing apart and a two-hander, taking both hands, swings once. A swing is the move its weapon's style
    /// has for the ask (<see cref="WeaponPart.Style"/>, <see cref="CombatStyleRecord"/>), timed as the look plays it
    /// (<see cref="Timings"/>), and only with weapons drawn (<see cref="WeaponReadiness"/>). While a swing is in its hit
    /// window, whatever finds what the weapon passes through (the look's blade, later a projectile) calls
    /// <see cref="Strike"/>: the weapon's blow (<see cref="WeaponPart.Hit"/>) is applied to the target's attributes, once
    /// per swing and target. Putting the weapons away, or taking the swinging one off, stops the swing.
    /// <para>
    /// A held move (a bow's draw, <see cref="CombatMove.Held"/>) is drawn out while the attack is held and let go by
    /// <see cref="Release"/>, as Skyrim's bow is: let go once the arrow is on the string (its window open), a weapon that
    /// shoots uses up one of its ammo (<see cref="WeaponPart.Ammo"/>) and looses it (<see cref="Loosed"/>), as hard as it
    /// was drawn (<see cref="Shot.Power"/>); let go sooner, it looses as soon as the arrow is on. What it looses strikes
    /// the first it reaches (<see cref="Strike(Shot, ActorId)"/>), the weapon's blow times its power.
    /// </para>
    /// Told as it happens (<see cref="SwingStarted"/>, <see cref="SwingHeld"/>, <see cref="SwingLetGo"/>,
    /// <see cref="Loosed"/>, <see cref="SwingEnded"/>) for the look, and on the stream (<see cref="AttackStarted"/>,
    /// <see cref="ShotLoosed"/>, <see cref="HitLanded"/>, <see cref="ActorKilled"/>) for the rest. Engine-free; the game ticks it.
    /// </summary>
    public sealed class ActorCombat : IDisposable
    {
        /// <summary>How hard a shot loosed the moment it is on the string flies and strikes, of a full draw's.</summary>
        public const float WeakestShot = 0.3f;

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

        /// <summary>A held swing came to the end of its window and stays there while held (a bow at full draw), for the look.</summary>
        public event Action<Swing> SwingHeld;

        /// <summary>A held swing was let go and plays on from the end of its window, for the look.</summary>
        public event Action<Swing> SwingLetGo;

        /// <summary>A weapon that shoots loosed what it shoots, for the look to fly.</summary>
        public event Action<Shot> Loosed;

        /// <summary>
        /// Attacks with what <paramref name="slot"/> holds (the right hand's sword); a held move (a bow's draw) is held
        /// until <see cref="Release"/>.
        /// </summary>
        public AttackOutcome TryAttack(RecordId slot) => TryMove(slot, CombatMoveKind.Attack);

        /// <summary>
        /// The attack asked for with <paramref name="slot"/> is released: its held swing (a bow's draw) lets go now, if its
        /// window is open, else as soon as it opens. Whether there was such a swing.
        /// </summary>
        public bool Release(RecordId slot)
        {
            foreach (var swing in _swings)
            {
                if (swing.Slot != slot || !swing.Holding)
                    continue;
                swing.Release();
                if (swing.WindowOpen)
                    LetGo(swing);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Lets <paramref name="deltaTime"/> seconds pass: swings come through their windows to their ends, a held one
        /// stopping at its window's end while held, and one let go before its window opened letting go as it opens.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return;
            for (var i = _swings.Count - 1; i >= 0; i--)
            {
                var swing = _swings[i];
                var wasStill = swing.IsHeldStill;
                swing.Advance(deltaTime);
                if (!wasStill && swing.IsHeldStill)
                    SwingHeld?.Invoke(swing);
                if (swing.Move.Held && !swing.Holding && !swing.Loosed && swing.WindowOpen)
                    LetGo(swing);
                if (!swing.IsOver || i >= _swings.Count || _swings[i] != swing)
                    continue;
                _swings.RemoveAt(i);
                SwingEnded?.Invoke(swing);
            }
        }

        /// <summary>
        /// <paramref name="swing"/>'s weapon passed through <paramref name="target"/>: its blow lands, if the swing is in
        /// its hit window, its weapon strikes with itself (not a bow: what it shoots strikes), the target can be struck and
        /// isn't the swinger, and this swing hasn't struck it yet. Whether it landed.
        /// </summary>
        public bool Strike(Swing swing, ActorId target)
        {
            if (swing == null)
                throw new ArgumentNullException(nameof(swing));
            if (!_swings.Contains(swing) || !swing.IsStriking || swing.Shoots || target == Owner)
                return false;
            if (!_combatants.TryGet(target, out var attributes) || IsDead(attributes) || !swing.MarkStruck(target))
                return false;

            Land(swing.Weapon.Record, 1f, target, attributes);
            return true;
        }

        /// <summary>
        /// What this actor shot reached <paramref name="target"/>: the weapon's blow lands, times the shot's power, if the
        /// target can be struck and isn't the shooter, and the shot hasn't struck anyone yet. Whether it landed.
        /// </summary>
        public bool Strike(Shot shot, ActorId target)
        {
            if (shot == null)
                throw new ArgumentNullException(nameof(shot));
            if (shot.Shooter != Owner || shot.Landed || target == Owner)
                return false;
            if (!_combatants.TryGet(target, out var attributes) || IsDead(attributes))
                return false;

            shot.Landed = true;
            Land(shot.Weapon, shot.Power, target, attributes);
            return true;
        }

        // The weapon's blow, `power` times as strong, on the target; told on the stream.
        void Land(BaseObjectRecord weapon, float power, ActorId target, ActorAttributes attributes)
        {
            var health = 0f;
            foreach (var effect in weapon.Weapon.Hit)
            {
                var applied = AttributeEffects.Apply(effect.Times(power), attributes);
                if (effect.Kind == EffectKind.Damage && effect.Attribute == CoreAttributes.Health)
                    health += applied;
            }

            _events?.Publish(new HitLanded(Owner, target, weapon.Id, health));
            if (IsDead(attributes))
                _events?.Publish(new ActorKilled(target, Owner));
        }

        // A held swing lets go: one that shoots uses up one of its ammo and looses it, as hard as it was drawn (none
        // left: it lets go of nothing). It plays on from the end of its window.
        void LetGo(Swing swing)
        {
            var power = WeakestShot + (1f - WeakestShot) * swing.Pull;
            swing.Loose();
            SwingLetGo?.Invoke(swing);
            var ammo = swing.Weapon.Record.Weapon.Ammo;
            if (ammo == null || !_equipment.TryUse(ammo.Value, out var used))
                return;
            var shot = new Shot(Owner, swing, used, power);
            Loosed?.Invoke(shot);
            _events?.Publish(new ShotLoosed(Owner, swing.Weapon.Item, used.Id, power));
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
            if (weapon.Ammo.HasValue && _equipment.In(weapon.Ammo.Value) == null)
                return AttackOutcome.NoAmmo;
            foreach (var swinging in _swings)
            {
                if (Overlaps(swinging.Weapon.Fit, equipped.Fit))
                    return AttackOutcome.Busy;
            }

            if (move.StaminaCost > 0f && _attributes != null && _attributes.Set.Contains(CoreAttributes.Stamina) &&
                !_attributes.TrySpend(CoreAttributes.Stamina, move.StaminaCost))
                return AttackOutcome.Tired;

            var timing = Timings != null && Timings.TryGet(move.Animation, weapon.Ammo.HasValue, out var timed) ? timed : MoveTiming.Default;
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
