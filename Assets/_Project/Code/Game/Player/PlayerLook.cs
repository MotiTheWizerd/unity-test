using System;
using System.Numerics;
using Forge.Core.Geometry;

namespace Forge.Game.Player
{
    /// <summary>
    /// Where the player looks: a heading (compass degrees, which also turns the body) and a pitch (degrees, up positive),
    /// held short of straight up and down so the view never flips. Cameras read it; first person looks along it.
    /// </summary>
    public sealed class PlayerLook
    {
        public const float MaxPitch = 85f;

        public PlayerLook(float heading = 0f, float pitch = 0f) => Set(heading, pitch);

        /// <summary>Compass degrees in [0, 360): 0 north (+Z), 90 east (+X).</summary>
        public float Heading { get; private set; }

        /// <summary>Degrees above the horizon (negative: below), within ±<see cref="MaxPitch"/>.</summary>
        public float Pitch { get; private set; }

        /// <summary>Looks <paramref name="degrees"/> further: x to the right, y up.</summary>
        public void Turn(Vector2 degrees) => Set(Heading + degrees.X, Pitch + degrees.Y);

        public void Set(float heading, float pitch)
        {
            Heading = float.IsNaN(heading) || float.IsInfinity(heading) ? 0f : Compass.Normalize(heading);
            Pitch = float.IsNaN(pitch) ? 0f : Math.Max(-MaxPitch, Math.Min(MaxPitch, pitch));
        }
    }
}
