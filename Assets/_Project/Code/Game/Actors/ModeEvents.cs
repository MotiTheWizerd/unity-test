using Forge.Game.Events;

namespace Forge.Game.Actors
{
    /// <summary>An actor's <see cref="Mode"/> changed: it went from <see cref="From"/> to <see cref="To"/> (a fight began, or it is at ease again).</summary>
    public readonly struct ModeChanged : IGameEvent
    {
        public ModeChanged(ActorId actor, Mode from, Mode to)
        {
            Actor = actor;
            From = from;
            To = to;
        }

        public ActorId Actor { get; }

        public Mode From { get; }

        public Mode To { get; }

        public override string ToString() => $"{Actor}: {From} → {To}";
    }
}
