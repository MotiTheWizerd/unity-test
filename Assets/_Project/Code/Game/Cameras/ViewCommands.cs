namespace Forge.Game.Cameras
{
    /// <summary>What the player asks of the view this frame: switch first and third person, or move the camera nearer or farther.</summary>
    public readonly struct ViewCommands
    {
        public static readonly ViewCommands None = default;

        /// <param name="toggle">Switch between first and third person (pressed this frame).</param>
        /// <param name="zoom">Wheel notches: positive nearer, negative farther.</param>
        public ViewCommands(bool toggle, float zoom)
        {
            Toggle = toggle;
            Zoom = zoom;
        }

        public bool Toggle { get; }

        public float Zoom { get; }
    }
}
