using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Terrain
{
    /// <summary>
    /// A chunk's ground paint in memory: for each of its <see cref="Resolution"/>² texels (one per square meter), how
    /// much of each of its <see cref="ChunkPaint.Layers"/> shows there, as bytes that add up to <see cref="Full"/>.
    /// Immutable. Painting builds a new one from amounts (<see cref="FromWeights"/>), which keeps the bytes exact and
    /// the layer list to the layers that show.
    /// </summary>
    public sealed class GroundPaint : ChunkPaint
    {
        public const int Resolution = WorldGrid.PaintResolution;

        public const int TexelCount = Resolution * Resolution;

        /// <summary>The weights of each texel add up to this.</summary>
        public const byte Full = 255;

        // Layer-major: all texels of the first layer (row-major by Z then X), then the second's, and so on.
        readonly byte[] _weights;

        /// <param name="weights">Layer-major, row-major by Z then X; each texel's weights add up to <see cref="Full"/>.</param>
        public GroundPaint(IReadOnlyList<RecordId> layers, byte[] weights)
            : base(layers)
        {
            if (weights == null)
                throw new ArgumentNullException(nameof(weights));
            if (weights.Length != Layers.Count * TexelCount)
                throw new ArgumentException($"Expected {Layers.Count * TexelCount} weights ({Layers.Count} layers of {TexelCount} texels) but got {weights.Length}.", nameof(weights));

            for (var texel = 0; texel < TexelCount; texel++)
            {
                var sum = 0;
                for (var layer = 0; layer < Layers.Count; layer++)
                    sum += weights[layer * TexelCount + texel];
                if (sum != Full)
                    throw new ArgumentException($"The weights of texel {texel % Resolution}, {texel / Resolution} add up to {sum}, not {Full}.", nameof(weights));
            }

            _weights = (byte[])weights.Clone();
        }

        // Takes ownership of weights already known to be valid, without a copy or a check.
        GroundPaint(IReadOnlyList<RecordId> layers, byte[] weights, bool trusted)
            : base(layers)
        {
            _weights = weights;
        }

        /// <summary>How much of layer <paramref name="layer"/> (an index into <see cref="ChunkPaint.Layers"/>) texel (x, z) shows, 0 to <see cref="Full"/>.</summary>
        public byte this[int layer, int x, int z] => _weights[layer * TexelCount + z * Resolution + x];

        /// <summary>The index of <paramref name="layer"/> in <see cref="ChunkPaint.Layers"/>, or -1 when the chunk isn't painted with it.</summary>
        public int IndexOf(RecordId layer)
        {
            for (var i = 0; i < Layers.Count; i++)
            {
                if (Layers[i] == layer)
                    return i;
            }

            return -1;
        }

        /// <summary>The layer showing most at texel (x, z); the first of them on a tie.</summary>
        public RecordId DominantAt(int x, int z)
        {
            var best = 0;
            for (var layer = 1; layer < Layers.Count; layer++)
            {
                if (this[layer, x, z] > this[best, x, z])
                    best = layer;
            }

            return Layers[best];
        }

        /// <summary>Copy of the weights, layer-major.</summary>
        public byte[] ToArray() => (byte[])_weights.Clone();

        public override GroundPaint Load() => this;

        /// <summary>A chunk painted all over with one layer.</summary>
        public static GroundPaint Uniform(RecordId layer)
        {
            var weights = new byte[TexelCount];
            for (var i = 0; i < weights.Length; i++)
                weights[i] = Full;
            return new GroundPaint(new[] { layer }, weights, trusted: true);
        }

        /// <summary>
        /// Paint from amounts: <paramref name="weights"/> holds, layer-major, how much of each of
        /// <paramref name="layers"/> each texel shows, in any non-negative measure; each texel is scaled to
        /// <see cref="Full"/>, rounded so its bytes add up exactly. Layers that show nowhere are left out, and when
        /// more than <see cref="ChunkPaint.MaxLayers"/> show, the faintest overall are let go (their share goes to the
        /// rest). A texel with no amount at all shows the strongest layer.
        /// </summary>
        public static GroundPaint FromWeights(IReadOnlyList<RecordId> layers, float[] weights)
        {
            if (layers == null)
                throw new ArgumentNullException(nameof(layers));
            if (weights == null)
                throw new ArgumentNullException(nameof(weights));
            if (layers.Count == 0)
                throw new ArgumentException("Paint needs at least one ground layer.", nameof(layers));
            if (layers.Distinct().Count() != layers.Count)
                throw new ArgumentException("Each ground layer is named once.", nameof(layers));
            if (weights.Length != layers.Count * TexelCount)
                throw new ArgumentException($"Expected {layers.Count * TexelCount} weights but got {weights.Length}.", nameof(weights));

            var totals = new double[layers.Count];
            for (var layer = 0; layer < layers.Count; layer++)
            for (var texel = 0; texel < TexelCount; texel++)
            {
                var w = weights[layer * TexelCount + texel];
                if (!(w >= 0f) || float.IsInfinity(w))
                    throw new ArgumentOutOfRangeException(nameof(weights), w, "Paint amounts are finite and not negative.");
                totals[layer] += w;
            }

            var kept = Strongest(totals, ChunkPaint.MaxLayers);
            if (kept.Count == 0)
                throw new ArgumentException("Nothing is painted: every amount is zero.", nameof(weights));

            var strongest = kept.OrderByDescending(layer => totals[layer]).ThenBy(layer => layer).First();
            var bytes = Quantize(weights, kept, kept.IndexOf(strongest));

            // A layer whose amounts all rounded to nothing doesn't show; it isn't kept either.
            var shown = Enumerable.Range(0, kept.Count).Where(k => AnyAbove(bytes, k)).ToList();
            if (shown.Count != kept.Count)
                bytes = Select(bytes, shown);

            return new GroundPaint(shown.Select(k => layers[kept[k]]).ToArray(), bytes, trusted: true);
        }

        // The indices of up to `max` layers with the largest totals above zero, in their original order.
        static List<int> Strongest(double[] totals, int max) =>
            Enumerable.Range(0, totals.Length)
                .Where(layer => totals[layer] > 0)
                .OrderByDescending(layer => totals[layer]).ThenBy(layer => layer)
                .Take(max)
                .OrderBy(layer => layer)
                .ToList();

        // Each texel's amounts of the kept layers as bytes adding up to Full: rounded down, then the bytes left over go
        // to the largest remainders, so no texel drifts from its proportions by more than one byte.
        static byte[] Quantize(float[] weights, List<int> kept, int fallback)
        {
            var count = kept.Count;
            var bytes = new byte[count * TexelCount];
            var shares = new double[count];
            for (var texel = 0; texel < TexelCount; texel++)
            {
                double sum = 0;
                for (var k = 0; k < count; k++)
                    sum += weights[kept[k] * TexelCount + texel];

                if (!(sum > 0))
                {
                    bytes[fallback * TexelCount + texel] = Full;
                    continue;
                }

                var given = 0;
                for (var k = 0; k < count; k++)
                {
                    shares[k] = weights[kept[k] * TexelCount + texel] / sum * Full;
                    var whole = (int)Math.Floor(shares[k]);
                    bytes[k * TexelCount + texel] = (byte)whole;
                    shares[k] -= whole;
                    given += whole;
                }

                for (var left = Full - given; left > 0; left--)
                {
                    var best = 0;
                    for (var k = 1; k < count; k++)
                    {
                        if (shares[k] > shares[best])
                            best = k;
                    }

                    bytes[best * TexelCount + texel]++;
                    shares[best] = -1;
                }
            }

            return bytes;
        }

        static bool AnyAbove(byte[] bytes, int layer)
        {
            for (var texel = 0; texel < TexelCount; texel++)
            {
                if (bytes[layer * TexelCount + texel] > 0)
                    return true;
            }

            return false;
        }

        static byte[] Select(byte[] bytes, List<int> layers)
        {
            var selected = new byte[layers.Count * TexelCount];
            for (var i = 0; i < layers.Count; i++)
                Array.Copy(bytes, layers[i] * TexelCount, selected, i * TexelCount, TexelCount);
            return selected;
        }

        /// <summary>Wraps decoded weights, which the caller guarantees are valid, without copying or checking them.</summary>
        internal static GroundPaint Trusted(IReadOnlyList<RecordId> layers, byte[] weights) => new GroundPaint(layers, weights, trusted: true);
    }
}
