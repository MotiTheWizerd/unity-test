namespace Forge.Game.Player
{
    /// <summary>Which way the player's body turns: with the look (first person), or toward where it goes (third person).</summary>
    public enum BodyFacing
    {
        /// <summary>The body faces the look's heading at once.</summary>
        Look,

        /// <summary>The body turns toward its movement, at the turn rate, and stays put when standing; the look orbits freely.</summary>
        Movement,
    }
}
