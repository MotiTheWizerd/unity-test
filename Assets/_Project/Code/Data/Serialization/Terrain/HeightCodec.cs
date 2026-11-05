using System;
using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Packs one chunk's heights into a terrain file block and back. Each height becomes a whole number of the file's
    /// steps, 16 bits each (1/256 m: under 4 mm, and round heights such as 50 m or 47.5 m stay exact). Each step is stored as its difference from a guess made from the
    /// neighbours already written (left + above − above-left), which is near zero on smooth land, slopes included.
    /// The low bytes and then the high bytes (mostly zero) go through Deflate. Flat land packs to a few dozen
    /// bytes, sculpted land to a fraction of its 4 bytes per sample.
    /// </summary>
    static class HeightCodec
    {
        /// <summary>The codec id stored with each block, so later codecs can sit beside this one.</summary>
        public const byte Id = 1;

        const int MaxStep = ushort.MaxValue;

        public static byte[] Encode(HeightField heights, float metersPerStep)
        {
            var resolution = heights.Resolution;
            var count = resolution * resolution;
            var steps = new ushort[count];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                steps[z * resolution + x] = Quantize(heights[x, z], metersPerStep);

            // The residuals as two byte planes: all low bytes, then all high bytes.
            var planes = new byte[count * 2];
            for (var i = 0; i < count; i++)
            {
                var zigzag = ZigZag((short)(ushort)(steps[i] - Guess(steps, i, resolution)));
                planes[i] = (byte)zigzag;
                planes[count + i] = (byte)(zigzag >> 8);
            }

            return BlockDeflate.Pack(planes);
        }

        /// <param name="metersPerStep">The file's step, positive.</param>
        /// <exception cref="DataValidationException">The block is damaged.</exception>
        public static HeightField Decode(byte[] block, int resolution, float metersPerStep)
        {
            var count = resolution * resolution;
            var planes = BlockDeflate.Unpack(block, count * 2, "heights");
            var steps = new ushort[count];
            var heights = new float[count];
            for (var i = 0; i < count; i++)
            {
                var zigzag = (ushort)(planes[i] | (planes[count + i] << 8));
                steps[i] = (ushort)(Guess(steps, i, resolution) + UnZigZag(zigzag));
                // Clamped to the terrain's range: a file's steps can reach beyond it.
                heights[i] = Math.Min(WorldGrid.MaxTerrainHeight, steps[i] * metersPerStep);
            }

            return HeightField.Trusted(resolution, heights);
        }

        /// <summary>The 16-bit step nearest to <paramref name="height"/>.</summary>
        public static ushort Quantize(float height, float metersPerStep) =>
            (ushort)Math.Max(0, Math.Min(MaxStep, (int)Math.Round(height / metersPerStep)));

        // The step predicted from the samples before index i (left, above, above-left), wrapping like the steps do.
        static int Guess(ushort[] steps, int i, int resolution)
        {
            var x = i % resolution;
            var z = i / resolution;
            if (x > 0 && z > 0)
                return steps[i - 1] + steps[i - resolution] - steps[i - resolution - 1];
            if (x > 0)
                return steps[i - 1];
            return z > 0 ? steps[i - resolution] : 0;
        }

        // Small residuals of either sign become small unsigned numbers: 0, -1, 1, -2 → 0, 1, 2, 3.
        static ushort ZigZag(short value) => (ushort)((value << 1) ^ (value >> 15));

        static short UnZigZag(ushort value) => (short)((value >> 1) ^ -(value & 1));
    }
}
