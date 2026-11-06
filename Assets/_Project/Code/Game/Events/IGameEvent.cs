namespace Forge.Game.Events
{
    /// <summary>
    /// Something that happened in the game, told on the <see cref="GameEvents"/> stream: a fact in the past tense
    /// (an actor fell, a resource ran out, an item was taken), never a request. Events are small readonly structs, so
    /// telling them makes no garbage; each names the actor it happened to, where there is one.
    /// </summary>
    public interface IGameEvent
    {
    }
}
