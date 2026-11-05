using System;
using System.Collections.Generic;
using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Packs one chunk's ground paint into a terrain file block and back. The block holds the layer count, then each
    /// layer's weights as a plane, every byte stored as its difference from the one to its left (zero across evenly
    /// painted ground), through Deflate. The layers themselves are named in the plugin's JSON; the count here only
    /// checks the two agree. A chunk painted with one layer packs to a few dozen bytes.
    /// </summary>
    static class PaintCodec
    {
        /// <summary>The codec id stored with each paint block, beside <see cref="HeightCodec.Id"/>.</summary>
        public const byte Id = 2;

        const int Resolution = GroundPaint.Resolution;

        public static byte[] Encode(GroundPaint paint)
        {
            var weights = paint.ToArray();
            var bytes = new byte[1 + weights.Length];
            bytes[0] = (byte)paint.Layers.Count;
            for (var i = 0; i < weights.Length; i++)
                bytes[1 + i] = i % Resolution == 0 ? weights[i] : unchecked((byte)(weights[i] - weights[i - 1]));
            return BlockDeflate.Pack(bytes);
        }

        /// <param name="layers">The layers the plugin's JSON names for this block.</param>
        /// <exception cref="DataValidationException">The block is damaged, or holds another number of layers.</exception>
        public static GroundPaint Decode(byte[] block, IReadOnlyList<RecordId> layers)
        {
            var count = layers.Count * GroundPaint.TexelCount;
            var bytes = BlockDeflate.Unpack(block, 1 + count, "paint");
            if (bytes[0] != layers.Count)
                throw new DataValidationException(new[] { $"A paint block holds {bytes[0]} layers, but its chunk names {layers.Count}." });

            var weights = new byte[count];
            for (var i = 0; i < count; i++)
                weights[i] = i % Resolution == 0 ? bytes[1 + i] : unchecked((byte)(weights[i - 1] + bytes[1 + i]));

            try
            {
                return new GroundPaint(layers, weights);
            }
            catch (ArgumentException ex)
            {
                throw new DataValidationException(new[] { $"A paint block is damaged: {ex.Message}" }, ex);
            }
        }
    }
}
