namespace Forge.Game.Motion
{
    /// <summary>
    /// A body that can crouch (<see cref="IMotor"/>s that can say so): shorter while crouched, so it
    /// passes under what it couldn't standing, and it stands back up only where there is room overhead. A body that
    /// isn't one (a horse, a test's) never crouches.
    /// </summary>
    public interface ICrouchingBody
    {
        /// <summary>Whether it is crouched now.</summary>
        bool Crouched { get; }

        /// <summary>Meters lower its top is crouched than standing, which the eyes come down by.</summary>
        float CrouchDepth { get; }

        /// <summary>Whether it could stand up where it is: nothing overhead in the way.</summary>
        bool CanStand { get; }

        /// <summary>Crouches it, or stands it up (whether or not there is room: ask <see cref="CanStand"/> first).</summary>
        void Crouch(bool crouched);
    }
}
