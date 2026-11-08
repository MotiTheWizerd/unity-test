namespace Forge.Play
{
    /// <summary>How a carried thing is held (<see cref="Carried"/>).</summary>
    public enum CarryHold
    {
        /// <summary>
        /// By the point it was taken at, every turn free, so the rest hangs and swings under its weight (the use key held:
        /// for dragging, swinging, throwing).
        /// </summary>
        Loose,

        /// <summary>
        /// By its middle, kept turned as it lay and turning only as the carrier turns, so it neither swings nor tips (the
        /// lift key held: a bowl stays a bowl, a bottle stays standing).
        /// </summary>
        Steady,
    }
}
