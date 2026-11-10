namespace Forge.Play.Sound
{
    /// <summary>
    /// The mix a sound plays through (Settings/Sound/Forge.mixer), so each kind is turned up or down, and later
    /// muffled, together. A cue's bus is its top folder under Content/Sounds when that names one (Footsteps), else
    /// <see cref="Effects"/>.
    /// </summary>
    public enum SoundBus
    {
        Effects,
        Combat,
        Foley,
        Footsteps,
        Ambience,
        Music,
        UI,
    }
}
