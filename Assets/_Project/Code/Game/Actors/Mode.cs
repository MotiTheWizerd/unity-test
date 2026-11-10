namespace Forge.Game.Actors
{
    /// <summary>
    /// What an actor is up to, the player's or anyone's: the one fact its animation, its behaviour and the music all read
    /// (<see cref="ActorModes"/>). More come as the game needs them (alert, fleeing), one each.
    /// </summary>
    public enum Mode
    {
        /// <summary>At ease: going about its day.</summary>
        Normal,

        /// <summary>Fighting someone (<see cref="ActorModes.OpponentsOf"/>), or just done and not yet at ease.</summary>
        Fight,
    }
}
