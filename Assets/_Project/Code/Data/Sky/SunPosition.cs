namespace Forge.Data.Sky
{
    /// <summary>Where the sun stands: degrees above the horizon (negative below it) and the compass bearing toward it.</summary>
    public readonly struct SunPosition
    {
        public SunPosition(float height, float bearing)
        {
            Height = height;
            Bearing = bearing;
        }

        /// <summary>Degrees above the horizon; negative at night.</summary>
        public float Height { get; }

        /// <summary>Compass bearing toward the sun: 0 north, 90 east, 180 south, 270 west.</summary>
        public float Bearing { get; }

        public bool IsUp => Height > 0f;
    }
}
