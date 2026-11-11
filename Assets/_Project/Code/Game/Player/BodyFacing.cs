namespace Forge.Game.Player
{
    /// <summary>
    /// Which way an actor's body turns: with the look (the player in first person), toward where it goes (the player
    /// in third person, a character about its day), or onto the look wherever it goes (a fighter squaring up to its
    /// foe).
    /// </summary>
    public enum BodyFacing
    {
        /// <summary>The body faces the look's heading at once.</summary>
        Look,

        /// <summary>The body turns toward its movement, at the turn rate, and stays put when standing; the look orbits freely.</summary>
        Movement,

        /// <summary>
        /// The body turns toward the look's heading at the turn rate, moving or standing, as it does aiming a bow: a
        /// character's mind looks at its opponent and the body squares up to it (later the player's lock-on).
        /// </summary>
        Target,
    }
}
