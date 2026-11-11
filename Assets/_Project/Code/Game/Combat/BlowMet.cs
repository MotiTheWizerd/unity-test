namespace Forge.Game.Combat
{
    /// <summary>
    /// Where a blow met the one it struck, as whatever saw it says (the look's blade, an arrow in flight): its raised
    /// guard itself (the shield), its body past the guard, or nowhere anyone saw (the guard's arc decides,
    /// <see cref="ActorCombat.GuardArc"/>).
    /// </summary>
    public enum BlowMet
    {
        /// <summary>Not seen: a raised guard stops it when it comes from where the guard faces.</summary>
        Anywhere,

        /// <summary>On the raised guard itself: the guard stops it, whichever way it came.</summary>
        Guard,

        /// <summary>On the body, past the guard: nothing stops it.</summary>
        Body,
    }
}
