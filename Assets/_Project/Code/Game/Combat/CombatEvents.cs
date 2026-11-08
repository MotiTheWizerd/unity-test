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

    /// <summary><see cref="Actor"/>'s <see cref="Weapon"/> loosed one <see cref="Ammo"/> (a bow, an arrow), <see cref="Power"/> of a full draw's.</summary>
    public readonly struct ShotLoosed : IGameEvent
    {
        public ShotLoosed(ActorId actor, RecordId weapon, RecordId ammo, float power)
        {
            Actor = actor;
            Weapon = weapon;
            Ammo = ammo;
            Power = power;
        }

        public ActorId Actor { get; }

        public RecordId Weapon { get; }

        public RecordId Ammo { get; }

        public float Power { get; }

        public override string ToString() => $"{Actor}: loosed {Ammo} from {Weapon} at {Power:P0}";
    }

    /// <summary>
    /// What <see cref="Actor"/> shot (one <see cref="Ammo"/>) landed: it stays there as <see cref="Placed"/>, to be taken
    /// back, or it broke (no placed object).
    /// </summary>
    public readonly struct ShotLanded : IGameEvent
    {
        public ShotLanded(ActorId actor, RecordId ammo, RecordId? placed)
        {
            Actor = actor;
            Ammo = ammo;
            Placed = placed;
        }

        public ActorId Actor { get; }

        public RecordId Ammo { get; }

        /// <summary>The placed object it now is, or null: it broke.</summary>
        public RecordId? Placed { get; }

        public bool Broke => Placed == null;

        public override string ToString() => Broke ? $"{Actor}'s {Ammo} broke" : $"{Actor}'s {Ammo} landed as {Placed}";
    }

    /// <summary>A blow landed: <see cref="Attacker"/>'s <see cref="Weapon"/> struck <see cref="Target"/>, taking <see cref="Damage"/> health.</summary>
    public readonly struct HitLanded : IGameEvent
    {
        public HitLanded(ActorId attacker, ActorId target, RecordId weapon, float damage)
        {
            Attacker = attacker;
            Target = target;
            Weapon = weapon;
            Damage = damage;
        }

        public ActorId Attacker { get; }

        public ActorId Target { get; }

        public RecordId Weapon { get; }

        /// <summary>Health the blow took (after nothing was left to take, less than it would have).</summary>
        public float Damage { get; }

        public override string ToString() => $"{Attacker} hit {Target} with {Weapon} for {Damage:0.#}";
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
