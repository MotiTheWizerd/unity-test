using Forge.Data;
using Forge.Game.Actors;
using Forge.Game.Events;

namespace Forge.Game.Player
{
    /// <summary>An actor took off in a jump.</summary>
    public readonly struct ActorJumped : IGameEvent
    {
        public ActorJumped(ActorId actor) => Actor = actor;

        public ActorId Actor { get; }

        public override string ToString() => $"{Actor}: jumped";
    }

    /// <summary>An actor ran out of stamina mid-sprint and is winded: it walks until it gets its breath back.</summary>
    public readonly struct ActorExhausted : IGameEvent
    {
        public ActorExhausted(ActorId actor) => Actor = actor;

        public ActorId Actor { get; }

        public override string ToString() => $"{Actor}: exhausted";
    }

    /// <summary>A winded actor got its breath back: it can run and sprint again.</summary>
    public readonly struct ActorRecovered : IGameEvent
    {
        public ActorRecovered(ActorId actor) => Actor = actor;

        public ActorId Actor { get; }

        public override string ToString() => $"{Actor}: recovered";
    }

    /// <summary>An actor could not make a move for want of an attribute (a jump without the stamina for it).</summary>
    public readonly struct ActorShortOf : IGameEvent
    {
        public ActorShortOf(ActorId actor, RecordId attribute)
        {
            Actor = actor;
            Attribute = attribute;
        }

        public ActorId Actor { get; }

        public RecordId Attribute { get; }

        public override string ToString() => $"{Actor}: short of {Attribute}";
    }

    /// <summary>An actor fell further than it falls unhurt, and lost <see cref="Damage"/> health for it.</summary>
    public readonly struct ActorFell : IGameEvent
    {
        public ActorFell(ActorId actor, float meters, float damage)
        {
            Actor = actor;
            Meters = meters;
            Damage = damage;
        }

        public ActorId Actor { get; }

        /// <summary>How far it fell, from the highest point off the ground to where it touched down.</summary>
        public float Meters { get; }

        /// <summary>The health the fall cost (what the actor had, at most).</summary>
        public float Damage { get; }

        public override string ToString() => $"{Actor}: fell {Meters:0.#} m, lost {Damage:0.#} health";
    }
}
