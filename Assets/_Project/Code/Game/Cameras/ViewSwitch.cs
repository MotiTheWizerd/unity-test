using System;

namespace Forge.Game.Cameras
{
    /// <summary>
    /// First or third person, switched as in Skyrim: the toggle (F) swaps them, the third-person camera keeping its
    /// distance; the wheel moves it nearer or farther, one notch nearer than its closest goes to first person, and one
    /// notch farther in first person comes out to third at its closest.
    /// </summary>
    public sealed class ViewSwitch
    {
        public ViewSwitch(FirstPersonCamera firstPerson, ThirdPersonCamera thirdPerson, bool startInThirdPerson = false)
        {
            FirstPerson = firstPerson ?? throw new ArgumentNullException(nameof(firstPerson));
            ThirdPerson = thirdPerson ?? throw new ArgumentNullException(nameof(thirdPerson));
            IsThirdPerson = startInThirdPerson;
        }

        public FirstPersonCamera FirstPerson { get; }

        public ThirdPersonCamera ThirdPerson { get; }

        public bool IsThirdPerson { get; private set; }

        /// <summary>The camera mode framing the player now.</summary>
        public ICameraMode Current => IsThirdPerson ? (ICameraMode)ThirdPerson : FirstPerson;

        public void Apply(ViewCommands commands)
        {
            if (commands.Toggle)
                Show(!IsThirdPerson);

            var zoom = commands.Zoom;
            if (zoom > 0f && IsThirdPerson && ThirdPerson.IsClosest)
                Show(thirdPerson: false);
            else if (zoom < 0f && !IsThirdPerson)
                Show(thirdPerson: true, ThirdPerson.Settings.MinDistance);
            else if (IsThirdPerson)
                ThirdPerson.Zoom(zoom);
        }

        void Show(bool thirdPerson, float? distance = null)
        {
            if (thirdPerson && !IsThirdPerson)
                ThirdPerson.Begin(distance);
            IsThirdPerson = thirdPerson;
        }
    }
}
