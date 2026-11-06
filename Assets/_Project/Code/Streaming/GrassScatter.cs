using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Streaming
{
    /// <summary>One tuft of grass on a chunk: where it stands (chunk-local meters, Y the ground), how tall, which way it faces, and which layer grew it.</summary>
    public readonly struct GrassTuft
    {
        public GrassTuft(float x, float y, float z, float height, float yaw, int layer)
        {
            X = x;
            Y = y;
            Z = z;
            Height = height;
            Yaw = yaw;
            Layer = layer;
        }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        public float Height { get; }

        /// <summary>Turn around the vertical axis in radians, [0, 2π).</summary>
        public float Yaw { get; }

        /// <summary>Index into <see cref="GrassPatch.Layers"/>: the ground layer that grew it.</summary>
        public int Layer { get; }
    }

    /// <summary>The grass on one chunk: its tufts, and the ground layers they grew from.</summary>
    public sealed class GrassPatch
    {
        public static GrassPatch Empty { get; } = new GrassPatch(Array.Empty<RecordId>(), Array.Empty<GrassTuft>());

        public GrassPatch(IReadOnlyList<RecordId> layers, IReadOnlyList<GrassTuft> tufts)
        {
            Layers = layers ?? throw new ArgumentNullException(nameof(layers));
            Tufts = tufts ?? throw new ArgumentNullException(nameof(tufts));
        }

        public IReadOnlyList<RecordId> Layers { get; }

        public IReadOnlyList<GrassTuft> Tufts { get; }
    }

    /// <summary>
    /// Grows a chunk's grass from its ground paint: each square meter grows, for each layer painted there that has
    /// grass (<see cref="GroundLayerRecord.Grass"/>), its density times how much of it shows. Unpainted ground counts as
    /// the unpainted ground layer. Tufts stand on the ground (heights bilinear between samples), none under the sea.
    /// Deterministic: the same chunk always grows the same grass, so it doesn't shuffle when rebuilt or streamed back.
    /// </summary>
    public static class GrassScatter
    {
        /// <summary>Each tuft is this much shorter or taller than its layer's height, at most.</summary>
        public const float HeightVariation = 0.3f;

        const float TwoPi = (float)(Math.PI * 2);

        /// <param name="grassOf">The grass of a ground layer, or null when it grows none (or isn't known).</param>
        /// <param name="unpaintedGround">The layer unpainted ground counts as; null grows nothing there.</param>
        /// <param name="waterLevel">The sea's surface; ground below it grows nothing. Null for no sea.</param>
        public static GrassPatch Grow(TerrainChunkRecord chunk, Func<RecordId, GrassSettings> grassOf, RecordId? unpaintedGround, float? waterLevel)
        {
            if (chunk == null)
                throw new ArgumentNullException(nameof(chunk));
            if (grassOf == null)
                throw new ArgumentNullException(nameof(grassOf));

            var paint = chunk.Paint?.Load();
            var layers = paint != null ? paint.Layers : (unpaintedGround is RecordId bare ? new[] { bare } : Array.Empty<RecordId>());
            var grass = new GrassSettings[layers.Count];
            var any = false;
            for (var i = 0; i < layers.Count; i++)
            {
                grass[i] = grassOf(layers[i]);
                any |= grass[i] != null;
            }

            if (!any)
                return GrassPatch.Empty;

            var heights = chunk.Heights.Load();
            var spacing = WorldGrid.ChunkSize / (heights.Resolution - 1);
            var texel = WorldGrid.ChunkSize / GroundPaint.Resolution;
            var tufts = new List<GrassTuft>();

            for (var z = 0; z < GroundPaint.Resolution; z++)
            for (var x = 0; x < GroundPaint.Resolution; x++)
            {
                for (var layer = 0; layer < layers.Count; layer++)
                {
                    var settings = grass[layer];
                    if (settings == null)
                        continue;
                    var share = paint != null ? paint[layer, x, z] / (float)GroundPaint.Full : 1f;
                    if (share <= 0f)
                        continue;

                    // A whole number of tufts, plus one more with the chance of the fraction left over.
                    var random = new TexelRandom(chunk.Coord, x, z, layer);
                    var expected = settings.Density * share * texel * texel;
                    var count = (int)expected + (random.Next() < expected - (int)expected ? 1 : 0);
                    for (var i = 0; i < count; i++)
                    {
                        var px = (x + random.Next()) * texel;
                        var pz = (z + random.Next()) * texel;
                        var ground = HeightAt(heights, spacing, px, pz);
                        var height = settings.Height * (1f + (random.Next() * 2f - 1f) * HeightVariation);
                        var yaw = random.Next() * TwoPi;
                        if (waterLevel is float sea && ground < sea)
                            continue;
                        tufts.Add(new GrassTuft(px, ground, pz, height, yaw, layer));
                    }
                }
            }

            return new GrassPatch(layers, tufts);
        }

        // Bilinear between the chunk's own samples; the chunk spans samples 0..Resolution-1 on both axes.
        static float HeightAt(HeightField heights, float spacing, float x, float z)
        {
            var last = heights.Resolution - 1;
            var gx = Math.Min(x / spacing, last);
            var gz = Math.Min(z / spacing, last);
            var x0 = Math.Min((int)gx, last - 1);
            var z0 = Math.Min((int)gz, last - 1);
            var fx = gx - x0;
            var fz = gz - z0;
            var top = heights[x0, z0] + (heights[x0 + 1, z0] - heights[x0, z0]) * fx;
            var bottom = heights[x0, z0 + 1] + (heights[x0 + 1, z0 + 1] - heights[x0, z0 + 1]) * fx;
            return top + (bottom - top) * fz;
        }

        /// <summary>A small random stream seeded by where it is, so every texel's grass is the same on every build.</summary>
        struct TexelRandom
        {
            uint _state;

            public TexelRandom(ChunkCoord chunk, int x, int z, int layer)
            {
                _state = Mix(Mix(Mix(Mix(0x9E3779B9u ^ (uint)chunk.X) ^ (uint)chunk.Z) ^ (uint)(x | z << 8)) ^ (uint)layer);
            }

            /// <summary>The next number in [0, 1).</summary>
            public float Next()
            {
                _state = Mix(_state + 0x6D2B79F5u);
                return (_state >> 8) * (1f / (1 << 24));
            }

            // A strong 32-bit integer hash (lowbias32), so neighbouring seeds give unrelated streams.
            static uint Mix(uint h)
            {
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
