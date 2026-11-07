using Forge.Game.Actors;

namespace Forge.Game.Interaction
{
    /// <summary>
    /// One thing an actor can do to a placed object with the use key: take it (<c>TakeInteraction</c>); later open a door,
    /// search a container, talk. Whether it applies is read from the object's parts, so a new kind of thing is a new
    /// interaction and a part, never a change to the focus or the prompt.
    /// </summary>
    public interface IInteraction
    {
        /// <summary>What it would do with <paramref name="target"/> for <paramref name="actor"/>, or null: nothing.</summary>
        InteractionOffer Offer(InteractionTarget target, ActorId actor);

        /// <summary>Does it; called only with a target it made an offer for, in the same frame.</summary>
        void Perform(InteractionTarget target, ActorId actor);
    }
}
