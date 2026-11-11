using System;
using System.Collections.Generic;
using System.Numerics;
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
    /// the first it reaches (<see cref="Strike(Shot, ActorId, Vector3?)"/>), the weapon's blow times its power.
    /// </para>
    /// <para>
    /// A hand is used as its weapon's style says (<see cref="TryUse"/>): attacked with, or, a shield, raised as a block
    /// (<see cref="CombatMoveKind.Block"/>, held like a draw). It is the actor's guard (<see cref="IGuard"/>): a blow
    /// coming at it from within <see cref="GuardArc"/> of where it faces (<see cref="Facing"/>) while the block is up is
    /// stopped as much as the shield's <see cref="BlockPart"/> says, for stamina; too little stamina breaks the guard.
    /// Whoever sees where a blow met says so (<see cref="BlowMet"/>): on the raised shield itself it is blocked whichever
    /// way it came, on the body past it it isn't, and unseen the arc decides.
    /// </para>
    /// <para>
    /// A blow that lands is felt (<see cref="ImpactSeconds"/>, hit-stop): the swing that struck and everything the struck
    /// one does stop a moment (<see cref="Stall"/>, <see cref="IGuard.Jolt"/>, <see cref="Stalled"/> for the looks to
    /// freeze); a blade a guard stopped, or that met a wall (<see cref="Bounce"/>), comes back over
    /// <see cref="RecoilSeconds"/>, its blow spent (<see cref="Recoiled"/>).
    /// </para>
    /// Told as it happens (<see cref="SwingStarted"/>, <see cref="SwingHeld"/>, <see cref="SwingLetGo"/>,
    /// <see cref="Loosed"/>, <see cref="Blocked"/>, <see cref="Stalled"/>, <see cref="Recoiled"/>, <see cref="SwingEnded"/>)
    /// for the look, and on the stream
    /// (<see cref="AttackStarted"/>, <see cref="ShotLoosed"/>, <see cref="HitLanded"/>, <see cref="HitBlocked"/>,
    /// <see cref="GuardBroken"/>, <see cref="ActorKilled"/>) for the rest. Engine-free; the game ticks it.
    /// </summary>
    public sealed class ActorCombat : IDisposable, IGuard
    {
        /// <summary>How hard a shot loosed the moment it is on the string flies and strikes, of a full draw's.</summary>
        public const float WeakestShot = 0.3f;

        /// <summary>Degrees either side of where it faces that a raised guard covers.</summary>
        public const float GuardArc = 60f;

        /// <summary>Seconds a blow that meets someone stops the one who struck and the one struck (hit-stop): its weight.</summary>
        public const float ImpactSeconds = 0.08f;

        /// <summary>Seconds a blade that met what it can't cut through (a raised guard, a wall) takes to come back before its swing is over.</summary>
        public const float RecoilSeconds = 0.3f;

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

        /// <summary>Whether it draws a weapon that shoots (a bow), from the draw's start until it is over: it aims.</summary>
        public bool Aiming
        {
            get
            {
                foreach (var swing in _swings)
                {
                    if (swing.Shoots)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// How far a weapon that shoots is drawn while its attack is held: 0 with none held (or the arrow not yet on the
        /// string) to 1 at full draw (<see cref="Swing.Pull"/>); 0 again once let go.
        /// </summary>
        public float DrawPull
        {
            get
            {
                foreach (var swing in _swings)
                {
                    if (swing.Shoots && swing.Holding)
                        return swing.Pull;
                }

                return 0f;
            }
        }

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
        /// A raised guard (the swing raising it) stopped a blow, for the look to jolt it by its style's
        /// <see cref="CombatMoveKind.Blocked"/> move (null when the style has none).
        /// </summary>
        public event Action<Swing, CombatMove> Blocked;

        /// <summary>A blow's impact stopped it for so many seconds (it struck, or was struck), for the look to freeze as long.</summary>
        public event Action<float> Stalled;

        /// <summary>A swing's blade met what it can't cut through and comes back (<see cref="RecoilSeconds"/>), for the look to play it back.</summary>
        public event Action<Swing> Recoiled;

        /// <summary>
        /// Which way the actor faces (scene space; only its level part counts), which its guard covers; until set, its
        /// guard covers every way.
        /// </summary>
        public Func<Vector3> Facing { get; set; }

        /// <summary>
        /// The move its death plays: its weapon's style's (<see cref="CombatMoveKind.Death"/>: a sword's, a bow's), of
        /// the first thing it holds whose style has one; null: the look's own.
        /// </summary>
        public string DeathAnimation
        {
            get
            {
                foreach (var equipped in _equipment.All)
                {
                    var move = StyleMove(equipped, CombatMoveKind.Death);
                    if (move != null)
                        return move.Animation;
                }

                return null;
            }
        }

        /// <summary>The block raised now, its guard up (a shield in its window); null when none is.</summary>
        public Swing Guard
        {
            get
            {
                foreach (var swing in _swings)
                {
                    if (swing.Guards && swing.IsStriking)
                        return swing;
                }

                return null;
            }
        }

        /// <summary>
        /// Attacks with what <paramref name="slot"/> holds (the right hand's sword); a held move (a bow's draw) is held
        /// until <see cref="Release"/>.
        /// </summary>
        public AttackOutcome TryAttack(RecordId slot) => TryMove(slot, CombatMoveKind.Attack);

        /// <summary>
        /// Uses what <paramref name="slot"/> holds as its style says, Skyrim's way: attacks with a weapon, raises a shield
        /// (its block, held until <see cref="Release"/>).
        /// </summary>
        public AttackOutcome TryUse(RecordId slot)
        {
            var outcome = TryMove(slot, CombatMoveKind.Attack);
            return outcome == AttackOutcome.NoMove ? TryMove(slot, CombatMoveKind.Block) : outcome;
        }

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
                var wasOpen = swing.WindowOpen;
                swing.Advance(deltaTime);
                if (!wasStill && swing.IsHeldStill)
                    SwingHeld?.Invoke(swing);
                if (!wasOpen && swing.WindowOpen)
                    _events?.Publish(new MoveWindowOpened(Owner, swing.Slot, swing.Weapon.Item, swing.Move.Animation, swing.Measure));
                if (swing.Move.Held && !swing.Holding && !swing.Loosed && swing.WindowOpen)
                    LetGo(swing);
                if (!swing.IsOver || i >= _swings.Count || _swings[i] != swing)
                    continue;
                _swings.RemoveAt(i);
                SwingEnded?.Invoke(swing);
            }
        }

        /// <summary>
        /// <paramref name="swing"/>'s weapon passed through <paramref name="target"/>, travelling
        /// <paramref name="direction"/> (scene space; null: from wherever), meeting it where <paramref name="met"/> says:
        /// its blow lands, met first by the target's guard, if the swing is in its hit window, its weapon strikes with
        /// itself (not a bow: what it shoots strikes; not a block), the target can be struck and isn't the swinger, and
        /// this swing hasn't struck it yet. Landed, both feel it a moment; a guard that stopped it bounces it back.
        /// Whether it landed.
        /// </summary>
        public bool Strike(Swing swing, ActorId target, Vector3? direction = null, BlowMet met = BlowMet.Anywhere)
        {
            if (swing == null)
                throw new ArgumentNullException(nameof(swing));
            if (!_swings.Contains(swing) || !swing.IsStriking || swing.Shoots || swing.Guards || target == Owner)
                return false;
            if (!_combatants.TryGet(target, out var attributes) || IsDead(attributes) || !swing.MarkStruck(target))
                return false;

            Land(swing.Weapon.Record, 1f, target, attributes, direction, met, swing);
            return true;
        }

        /// <summary>
        /// What this actor shot reached <paramref name="target"/>, flying <paramref name="direction"/> (scene space; null:
        /// from wherever), meeting it where <paramref name="met"/> says: the weapon's blow lands, times the shot's power,
        /// met first by the target's guard, if the target can be struck and isn't the shooter, and the shot hasn't struck
        /// anyone yet. Whether it landed.
        /// </summary>
        public bool Strike(Shot shot, ActorId target, Vector3? direction = null, BlowMet met = BlowMet.Anywhere)
        {
            if (shot == null)
                throw new ArgumentNullException(nameof(shot));
            if (shot.Shooter != Owner || shot.Landed || target == Owner)
                return false;
            if (!_combatants.TryGet(target, out var attributes) || IsDead(attributes))
                return false;

            shot.Landed = true;
            Land(shot.Weapon, shot.Power, target, attributes, direction, met, null);
            return true;
        }

        /// <summary>
        /// <paramref name="swing"/>'s blade met what it can't cut through and no one's (a wall, a rock): it stops a moment
        /// there and comes back, its blow spent. Whether it did (only in its hit window, a blade's swing).
        /// </summary>
        public bool Bounce(Swing swing)
        {
            if (swing == null)
                throw new ArgumentNullException(nameof(swing));
            if (!_swings.Contains(swing) || !swing.IsStriking || swing.Shoots || swing.Guards)
                return false;
            Impact(swing, bounces: true);
            return true;
        }

        /// <summary>
        /// Whatever it is doing stops for <paramref name="seconds"/> (a blow's impact: it struck, or was struck): every
        /// swing of its holds where it is meanwhile, and the look freezes as long (<see cref="Stalled"/>).
        /// </summary>
        public void Stall(float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
                return;
            foreach (var swing in _swings)
                swing.Stall(seconds);
            Stalled?.Invoke(seconds);
        }

        /// <inheritdoc/>
        public void Jolt(float seconds) => Stall(seconds);

        /// <inheritdoc/>
        public float Block(ActorId attacker, RecordId weapon, float damage, Vector3? direction, BlowMet met = BlowMet.Anywhere)
        {
            var guard = Guard;
            var block = guard?.Weapon.Record.Block;
            if (block == null || !Covers(direction, met))
                return 0f;

            var stopped = Math.Max(0f, damage) * block.Stops;
            if (_attributes != null && _attributes.Set.Contains(CoreAttributes.Stamina) &&
                !_attributes.TrySpend(CoreAttributes.Stamina, stopped * block.Effort))
            {
                Stop(guard);
                _events?.Publish(new GuardBroken(Owner, attacker));
                return 0f;
            }

            Blocked?.Invoke(guard, StyleMove(guard.Weapon, CombatMoveKind.Blocked));
            _events?.Publish(new HitBlocked(attacker, Owner, weapon, stopped));
            return block.Stops;
        }

        // The move of `kind` of the style `equipped` is fought with; null when it has none.
        CombatMove StyleMove(EquippedItem equipped, CombatMoveKind kind)
        {
            var style = equipped.Record.Weapon?.Style;
            return style.HasValue && _records.TryGet(style.Value, out var record) && record is CombatStyleRecord combatStyle &&
                   combatStyle.TryGetMove(kind, out var move)
                ? move
                : null;
        }

        // Whether the guard takes a blow that met it where `met` says: on itself, always; on the body past it, never; unseen,
        // when it travels `direction` at where it faces, within the guard's arc (level only).
        bool Covers(Vector3? direction, BlowMet met)
        {
            if (met != BlowMet.Anywhere)
                return met == BlowMet.Guard;
            if (direction == null || Facing == null)
                return true;
            var facing = Facing();
            var at = new Vector2(facing.X, facing.Z);
            var from = -new Vector2(direction.Value.X, direction.Value.Z);
            if (at.LengthSquared() < 1e-8f || from.LengthSquared() < 1e-8f)
                return true;
            return Vector2.Dot(Vector2.Normalize(at), Vector2.Normalize(from)) >= (float)Math.Cos(GuardArc * Math.PI / 180.0);
        }

        // The weapon's blow, `power` times as strong, on the target, less what its guard stops of the harm to its health;
        // the target jolted by it, and the swing that dealt it (none for a shot) stopped a moment in it, bouncing off a
        // guard that stopped it; told on the stream.
        void Land(BaseObjectRecord weapon, float power, ActorId target, ActorAttributes attributes, Vector3? direction, BlowMet met, Swing swing)
        {
            var guarded = _combatants.TryGetGuard(target, out var guard);
            var stops = guarded ? guard.Block(Owner, weapon.Id, HealthDamage(weapon, power), direction, met) : 0f;
            if (guarded)
                guard.Jolt(ImpactSeconds);
            if (swing != null)
                Impact(swing, bounces: stops > 0f);
            var health = 0f;
            foreach (var effect in weapon.Weapon.Hit)
            {
                var harmsHealth = effect.Kind == EffectKind.Damage && effect.Attribute == CoreAttributes.Health;
                var factor = harmsHealth ? power * (1f - stops) : power;
                if (!(factor > 0f))
                    continue;
                var applied = AttributeEffects.Apply(effect.Times(factor), attributes);
                if (harmsHealth)
                    health += applied;
            }

            _events?.Publish(new HitLanded(Owner, target, weapon.Id, health));
            if (IsDead(attributes))
                _events?.Publish(new ActorKilled(target, Owner));
        }

        // A blade's impact: it stops a moment where it met (and so does all it does), and, bouncing, comes back over
        // RecoilSeconds, its window closed.
        void Impact(Swing swing, bool bounces)
        {
            Stall(ImpactSeconds);
            if (!bounces || swing.Recoiling)
                return;
            swing.Recoil(RecoilSeconds);
            Recoiled?.Invoke(swing);
        }

        // What `weapon`'s blow, `power` times as strong, would take from health.
        static float HealthDamage(BaseObjectRecord weapon, float power)
        {
            var damage = 0f;
            foreach (var effect in weapon.Weapon.Hit)
            {
                if (effect.Kind == EffectKind.Damage && effect.Attribute == CoreAttributes.Health)
                    damage += effect.Amount * power;
            }

            return damage;
        }

        // A held swing lets go: one that shoots uses up one of its ammo and looses it, as hard as it was drawn (none
        // left: it lets go of nothing). It plays on from the end of its window, or into its move's release.
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
            _events?.Publish(new ShotLoosed(Owner, swing.Weapon.Item, used.Id, power, swing.Move.Release ?? swing.Move.Animation));
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

            var timing = Timings != null && Timings.TryGet(move.Animation, Swing.MeasureOf(move, weapon), out var timed) ? timed : MoveTiming.Default;
            var release = move.Release != null && Timings != null && Timings.TryGetRelease(move.Release, out var seconds) ? seconds : (float?)null;
            var swing = new Swing(slot, equipped, move, timing, release);
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
                if (_swings[i].Weapon == equipped)
                    Stop(_swings[i]);
            }
        }

        // Stops one swing at once (its weapon taken off, its guard broken).
        void Stop(Swing swing)
        {
            if (_swings.Remove(swing))
                SwingEnded?.Invoke(swing);
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
