using System;

namespace Forge.Game.Cameras
{
    /// <summary>How the first-person camera rides the player's eyes. Immutable.</summary>
    public sealed class FirstPersonSettings
    {
        public static readonly FirstPersonSettings Default = new FirstPersonSettings();

        /// <param name="headEase">Seconds the camera takes to follow two thirds of the way to where the head went, which
        /// steadies a stride's bob and a blow's lean; zero rides the head exactly.</param>
        /// <param name="nearClip">How near the camera sees, meters: near enough for the player's own hands and what they hold.</param>
        public FirstPersonSettings(float headEase = 0.1f, float nearClip = 0.05f)
        {
            if (!(headEase >= 0f) || float.IsInfinity(headEase))
                throw new ArgumentOutOfRangeException(nameof(headEase), headEase, "Must be a number of seconds, zero or more.");
            if (!(nearClip > 0f) || float.IsInfinity(nearClip))
                throw new ArgumentOutOfRangeException(nameof(nearClip), nearClip, "Must be a positive number.");
            HeadEase = headEase;
            NearClip = nearClip;
        }

        /// <summary>Seconds the camera takes to follow two thirds of the way to where the head went; zero rides it exactly.</summary>
        public float HeadEase { get; }

        /// <summary>How near the camera sees, meters.</summary>
        public float NearClip { get; }
    }
}
