namespace Forge.Data.Sky
{
    /// <summary>A moon at one moment, from <see cref="MoonPath.At"/>: where it stands, its phase and how much of it is lit.</summary>
    public readonly struct MoonState
    {
        static readonly string[] PhaseNames =
        {
            "new", "waxing crescent", "first quarter", "waxing gibbous",
            "full", "waning gibbous", "last quarter", "waning crescent",
        };

        public MoonState(Moon moon, SkyPosition position, float phase, float lit)
        {
            Moon = moon;
            Position = position;
            Phase = phase;
            Lit = lit;
        }

        public Moon Moon { get; }

        public SkyPosition Position { get; }

        /// <summary>Where it is in its cycle, [0, 1): 0 new, 0.25 first quarter, 0.5 full, 0.75 last quarter.</summary>
        public float Phase { get; }

        /// <summary>The share of its face that is lit, [0, 1]: 0 new, 1 full.</summary>
        public float Lit { get; }

        /// <summary>The phase in words ("waxing crescent", "full"), each name covering an eighth of the cycle.</summary>
        public string PhaseName => PhaseNames[(int)(Phase * PhaseNames.Length + 0.5f) % PhaseNames.Length];
    }
}
