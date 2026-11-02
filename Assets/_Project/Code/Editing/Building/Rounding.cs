using System;

namespace Forge.Editing.Building
{
    /// <summary>Rounds what the commands report: centimetres and whole degrees are plenty, and keep replies short.</summary>
    static class Rounding
    {
        public static float Meters(float value) => (float)Math.Round(value, 2);

        public static float Coarse(float value) => (float)Math.Round(value, 1);

        public static float Degrees(float value) => (float)Math.Round(value) % 360f;
    }
}
