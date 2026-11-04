namespace Forge.Data.Sky
{
    /// <summary>Where something in the sky stands (the sun, a moon): degrees above the horizon (negative below it) and the compass bearing toward it.</summary>
    public readonly struct SkyPosition
    {
        public SkyPosition(float height, float bearing)
        {
            Height = height;
            Bearing = bearing;
        }

        /// <summary>Degrees above the horizon; negative when it has set.</summary>
        public float Height { get; }

        /// <summary>Compass bearing toward it: 0 north, 90 east, 180 south, 270 west.</summary>
        public float Bearing { get; }

        public bool IsUp => Height > 0f;
    }
}
