using System;
using System.Numerics;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// Smooth random variation over the ground, for land that should look grown rather than turned: value noise
    /// summed over octaves, each half the size and half as strong as the last. The same position always gives the
    /// same value (no state, nothing to save), so an edit computed twice comes out the same.
    /// </summary>
    public static class TerrainNoise
    {
        /// <summary>The noise at <paramref name="ground"/>, within [-1, 1].</summary>
        /// <param name="wavelength">Size of the largest features, in meters.</param>
        /// <param name="octaves">How many layers, each half the size of the last.</param>
        /// <param name="seed">Picks an unrelated pattern, so two uses of the noise don't line up.</param>
        public static float At(Vector2 ground, float wavelength, int octaves, int seed)
        {
            if (!(wavelength > 0f))
                throw new ArgumentOutOfRangeException(nameof(wavelength), wavelength, "The wavelength must be positive.");
            if (octaves < 1)
                throw new ArgumentOutOfRangeException(nameof(octaves), octaves, "At least one octave.");

            var sum = 0f;
            var total = 0f;
            var amplitude = 1f;
            var frequency = 1f / wavelength;
            for (var octave = 0; octave < octaves; octave++)
            {
                sum += amplitude * Value(ground.X * frequency, ground.Y * frequency, seed + octave * 7919);
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return sum / total;
        }

        // Random values on a one-unit lattice, eased between so the noise has no creases.
        static float Value(float x, float z, int seed)
        {
            var x0 = (int)Math.Floor(x);
            var z0 = (int)Math.Floor(z);
            var u = Ease(x - x0);
            var v = Ease(z - z0);

            var south = Lerp(Lattice(x0, z0, seed), Lattice(x0 + 1, z0, seed), u);
            var north = Lerp(Lattice(x0, z0 + 1, seed), Lattice(x0 + 1, z0 + 1, seed), u);
            return Lerp(south, north, v);
        }

        // A hash of the lattice point, within [-1, 1].
        static float Lattice(int x, int z, int seed)
        {
            unchecked
            {
                var h = (uint)x * 0x8DA6B343u ^ (uint)z * 0xD8163841u ^ (uint)seed * 0xCB1AB31Fu;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h / (float)uint.MaxValue * 2f - 1f;
            }
        }

        static float Ease(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
