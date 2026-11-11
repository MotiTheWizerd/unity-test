namespace Forge.Game.Player
{
    /// <summary>
    /// Which way an actor's body turns: with the look (the player in first person), toward where it goes (the player
    /// in third person, a character about its day), or onto a heading it is given wherever it goes (a fighter squaring
    /// up to its foe).
    /// </summary>
    public enum BodyFacing
    {
        /// <summary>The body faces the look's heading at once.</summary>
        Look,

        /// <summary>The body turns toward its movement, at the turn rate, and stays put when standing; the look orbits freely.</summary>
        Movement,

        /// <summary>
        /// The body turns toward <see cref="PlayerCharacter.TargetHeading"/> at the turn rate, moving or standing, as it
        /// does aiming a bow: a character's mind squares up to its opponent, the player's lock-on to its foe
        /// (<see cref="SoftLockOn"/>), while the look goes where it will.
        /// </summary>
        Target,
    }
}
