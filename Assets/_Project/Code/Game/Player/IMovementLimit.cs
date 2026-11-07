namespace Forge.Game.Player
{
    /// <summary>
    /// A rule over how an actor may move this frame, whatever it is told: too laden to run today, rooted by a spell or
    /// slowed by a wound later. Each lets the commands through, or less of them; the player's rules never know which
    /// limits there are.
    /// </summary>
    public interface IMovementLimit
    {
        /// <summary>The commands as this limit lets them through.</summary>
        PlayerCommands Allow(PlayerCommands commands);
    }
}
