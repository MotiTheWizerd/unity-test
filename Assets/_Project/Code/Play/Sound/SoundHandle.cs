namespace Forge.Play.Sound
{
    /// <summary>
    /// One play of a sound (<see cref="SoundPlayer.Play(SoundCue, UnityEngine.Vector3, float)"/>), to cut it short by
    /// (<see cref="SoundPlayer.Stop"/>): the voice it played on and which of that voice's plays it was, so a handle kept
    /// past its sound never stops the next one on the same voice.
    /// </summary>
    public readonly struct SoundHandle
    {
        /// <summary>No sound: nothing played.</summary>
        public static readonly SoundHandle None = new SoundHandle(-1, 0);

        public SoundHandle(int voice, int play)
        {
            Voice = voice;
            Play = play;
        }

        public int Voice { get; }

        public int Play { get; }

        /// <summary>Whether a sound started playing.</summary>
        public bool Started => Voice >= 0;
    }
}
