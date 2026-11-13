using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Events;

namespace Forge.Game.Combat
{
    /// <summary>An actor began a combat move with what it holds in <see cref="Slot"/> (a slash with the right hand's sword).</summary>
    public readonly struct AttackStarted : IGameEvent
    {
        public AttackStarted(ActorId actor, RecordId slot, RecordId weapon, CombatMoveKind move)
        {
            Actor = actor;
            Slot = slot;
            Weapon = weapon;
            Move = move;
        }

        public ActorId Actor { get; }

        public RecordId Slot { get; }

        /// <summary>The weapon: its base object.</summary>
        public RecordId Weapon { get; }

        public CombatMoveKind Move { get; }

        public override string ToString() => $"{Actor}: {Move} with {Weapon} ({Slot})";
    }

    /// <summary>
    /// An actor's combat move came to its moment (its window opened), as it is heard: a blow sweeping through its cut,
    /// hit or miss; a bow's string pulled back, the arrow on it; a shield coming up. <see cref="Animation"/> names the
    /// move (a combat style's "SwordSlash"), <see cref="Measure"/> says which of those it is.
    /// </summary>
    public readonly struct MoveWindowOpened : IGameEvent
    {
        public MoveWindowOpened(ActorId actor, RecordId slot, RecordId weapon, string animation, MoveMeasure measure)
        {
            Actor = actor;
            Slot = slot;
            Weapon = weapon;
            Animation = animation;
            Measure = measure;
        }

        public ActorId Actor { get; }

        public RecordId Slot { get; }

        /// <summary>The weapon: its base object.</summary>
        public RecordId Weapon { get; }

        /// <summary>The move's animation, which names it ("SwordSlash").</summary>
        public string Animation { get; }

        /// <summary>What its window is: a cut, a draw, a raise.</summary>
        public MoveMeasure Measure { get; }

        public override string ToString() => $"{Actor}: {Animation} with {Weapon} came to its {Measure} ({Slot})";
    }

    /// <summary>
    /// <see cref="Actor"/>'s <see cref="Weapon"/> loosed one <see cref="Ammo"/> (a bow, an arrow), <see cref="Power"/> of a
    /// full draw's; <see cref="Release"/> names how it let go (its move's release, "BowRelease", else the move's own).
    /// </summary>
    public readonly struct ShotLoosed : IGameEvent
    {
        public ShotLoosed(ActorId actor, RecordId weapon, RecordId ammo, float power, string release = null)
        {
            Actor = actor;
            Weapon = weapon;
            Ammo = ammo;
            Power = power;
            Release = release;
        }

        public ActorId Actor { get; }

        public RecordId Weapon { get; }

        public RecordId Ammo { get; }

        public float Power { get; }

        /// <summary>The animation it let go by ("BowRelease"); null when it isn't known.</summary>
        public string Release { get; }

        public override string ToString() => $"{Actor}: loosed {Ammo} from {Weapon} at {Power:P0}";
    }

    /// <summary>
    /// What <see cref="Actor"/> shot (one <see cref="Ammo"/>) landed: it stays in the world as <see cref="Placed"/>, to be
    /// taken back, or in someone (<see cref="LodgedIn"/>, carried by them from then on), or it broke.
    /// </summary>
    public readonly struct ShotLanded : IGameEvent
    {
        public ShotLanded(ActorId actor, RecordId ammo, RecordId? placed, ActorId? lodgedIn = null)
        {
            Actor = actor;
            Ammo = ammo;
            Placed = placed;
            LodgedIn = lodgedIn;
        }

        public ActorId Actor { get; }

        public RecordId Ammo { get; }

        /// <summary>The placed object it now is in the world, or null: it isn't (it broke, or it is in someone).</summary>
        public RecordId? Placed { get; }

        /// <summary>Who it stuck in and now carries it, or null: no one (it is in the world, or it broke).</summary>
        public ActorId? LodgedIn { get; }

        public bool Broke => Placed == null && LodgedIn == null;

        public override string ToString() =>
            Broke ? $"{Actor}'s {Ammo} broke"
            : LodgedIn != null ? $"{Actor}'s {Ammo} lodged in {LodgedIn}"
            : $"{Actor}'s {Ammo} landed as {Placed}";
    }

    /// <summary>
    /// A blow landed: <see cref="Attacker"/>'s <see cref="Weapon"/> struck <see cref="Target"/> (on its <see cref="Part"/>),
    /// taking <see cref="Damage"/> health; <see cref="Blocked"/> when the target's guard took some of it (told first as a
    /// <see cref="HitBlocked"/>).
    /// </summary>
    public readonly struct HitLanded : IGameEvent
    {
        public HitLanded(ActorId attacker, ActorId target, RecordId weapon, float damage, BodyPart part = BodyPart.None, bool blocked = false)
        {
            Attacker = attacker;
            Target = target;
            Weapon = weapon;
            Damage = damage;
            Part = part;
            Blocked = blocked;
        }

        public ActorId Attacker { get; }

        public ActorId Target { get; }

        public RecordId Weapon { get; }

        /// <summary>Health the blow took (after nothing was left to take, less than it would have).</summary>
        public float Damage { get; }

        /// <summary>Where on the target's body it met; <see cref="BodyPart.None"/> when that isn't known.</summary>
        public BodyPart Part { get; }

        /// <summary>Whether the target's guard took some of it (its shield met it), what got past landing.</summary>
        public bool Blocked { get; }

        public override string ToString() =>
            $"{Attacker} hit {Target}{(Part == BodyPart.None ? "" : $"'s {Part}")} with {Weapon} for {Damage:0.#}";
    }

    /// <summary>
    /// <see cref="Target"/> blocked <see cref="Attacker"/>'s blow with <see cref="Weapon"/>, its guard stopping
    /// <see cref="Stopped"/> health of it (what got through lands as a <see cref="HitLanded"/>).
    /// </summary>
    public readonly struct HitBlocked : IGameEvent
    {
        public HitBlocked(ActorId attacker, ActorId target, RecordId weapon, float stopped, RecordId guard = default)
        {
            Attacker = attacker;
            Target = target;
            Weapon = weapon;
            Stopped = stopped;
            Guard = guard;
        }

        public ActorId Attacker { get; }

        public ActorId Target { get; }

        /// <summary>The attacker's weapon: its base object.</summary>
        public RecordId Weapon { get; }

        /// <summary>What the target blocked with (its shield): its base object, what it is made of among the rest.</summary>
        public RecordId Guard { get; }

        /// <summary>Health the guard stopped.</summary>
        public float Stopped { get; }

        public override string ToString() => $"{Target} blocked {Attacker}'s {Weapon}, stopping {Stopped:0.#}";
    }

    /// <summary><see cref="Actor"/>'s guard broke under <see cref="By"/>'s blow: too little stamina left to hold it, the blow lands whole.</summary>
    public readonly struct GuardBroken : IGameEvent
    {
        public GuardBroken(ActorId actor, ActorId by)
        {
            Actor = actor;
            By = by;
        }

        public ActorId Actor { get; }

        public ActorId By { get; }

        public override string ToString() => $"{Actor}'s guard broken by {By}";
    }

    /// <summary>An actor's health ran out from a blow; <see cref="By"/> struck it.</summary>
    public readonly struct ActorKilled : IGameEvent
    {
        public ActorKilled(ActorId actor, ActorId by)
        {
            Actor = actor;
            By = by;
        }

        public ActorId Actor { get; }

        public ActorId By { get; }

        public override string ToString() => $"{Actor} killed by {By}";
    }
}
