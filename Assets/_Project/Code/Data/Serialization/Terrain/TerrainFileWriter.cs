using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Writes chunk heights and ground paint as a <see cref="TerrainFile"/>, one block per distinct heights or paint
    /// instance (chunks sharing one, like the flat land of a new worldspace, share the block). Blocks still stored in
    /// another terrain file are copied as packed, without decoding, so saving a large world costs little more than its
    /// changes. Heights come first, then paint, so terrain without paint writes the same file it always did.
    /// </summary>
    public static class TerrainFileWriter
    {
        /// <summary>
        /// The height step new files use: 1/256 m, under 4 mm, exact in binary so round heights (50 m, 47.5 m) are kept
        /// exactly; 65,535 steps reach 256 m, past <see cref="WorldGrid.MaxTerrainHeight"/>.
        /// </summary>
        public const float MetersPerStep = 1f / 256f;

        /// <summary>
        /// Writes <paramref name="heights"/> and <paramref name="paint"/> (none when null) to <paramref name="output"/>,
        /// which must be seekable, from its start.
        /// </summary>
        public static WrittenTerrain Write(Stream output, IEnumerable<ChunkHeights> heights, IEnumerable<ChunkPaint> paint = null)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (!output.CanSeek || !output.CanWrite)
                throw new ArgumentException("The output must be writable and seekable.", nameof(output));
            if (heights == null)
                throw new ArgumentNullException(nameof(heights));

            var blocks = new Dictionary<object, int>();
            var table = new MemoryStream();
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
            using (var tableWriter = new BinaryWriter(table))
            {
                output.Position = 0;
                writer.Write(new byte[TerrainFile.HeaderSize]);

                void WriteBlock(object value, byte[] block, int resolution, byte codec)
                {
                    blocks.Add(value, blocks.Count);
                    tableWriter.Write(output.Position);
                    tableWriter.Write(block.Length);
                    tableWriter.Write((ushort)resolution);
                    tableWriter.Write(codec);
                    tableWriter.Write((byte)0);
                    writer.Write(block);
                    hash.AppendData(block);
                }

                foreach (var chunk in heights)
                {
                    if (chunk == null)
                        throw new ArgumentException("Chunk heights can't be missing.", nameof(heights));
                    if (!blocks.ContainsKey(chunk))
                        WriteBlock(chunk, Pack(chunk), chunk.Resolution, HeightCodec.Id);
                }

                foreach (var chunk in paint ?? Array.Empty<ChunkPaint>())
                {
                    if (chunk == null)
                        throw new ArgumentException("Chunk paint can't be missing; leave unpainted chunks out.", nameof(paint));
                    if (!blocks.ContainsKey(chunk))
                        WriteBlock(chunk, Pack(chunk), GroundPaint.Resolution, PaintCodec.Id);
                }

                var tableOffset = output.Position;
                var tableBytes = table.ToArray();
                writer.Write(tableBytes);
                hash.AppendData(tableBytes);

                var header = new MemoryStream(TerrainFile.HeaderSize);
                using (var headerWriter = new BinaryWriter(header))
                {
                    headerWriter.Write(TerrainFile.Magic);
                    headerWriter.Write(TerrainFile.FormatVersion);
                    headerWriter.Write((ushort)0);
                    headerWriter.Write(MetersPerStep);
                    headerWriter.Write(blocks.Count);
                    headerWriter.Write(tableOffset);
                }

                var headerBytes = header.ToArray();
                output.Position = 0;
                writer.Write(headerBytes);
                hash.AppendData(headerBytes);
                output.Position = output.Length;
                writer.Flush();

                return new WrittenTerrain(blocks, Hex(hash.GetHashAndReset(), WrittenTerrain.HashLength));
            }
        }

        // Stored heights are copied packed when their file uses this format; anything else is encoded afresh.
        static byte[] Pack(ChunkHeights heights) =>
            heights is StoredHeights stored && stored.File.MetersPerStep == MetersPerStep
                ? stored.File.ReadRaw(stored.Index)
                : HeightCodec.Encode(heights.Load(), MetersPerStep);

        // Stored paint is copied packed: its codec doesn't depend on the file's height step.
        static byte[] Pack(ChunkPaint paint) =>
            paint is StoredPaint stored ? stored.File.ReadRaw(stored.Index) : PaintCodec.Encode(paint.Load());

        static string Hex(byte[] bytes, int length)
        {
            var text = new StringBuilder(length);
            for (var i = 0; text.Length < length; i++)
                text.Append(bytes[i].ToString("x2"));
            return text.ToString(0, length);
        }
    }

    /// <summary>What <see cref="TerrainFileWriter"/> wrote: the block of each chunk's heights and paint, and the content's hash.</summary>
    public sealed class WrittenTerrain : ITerrainBlockWriter
    {
        /// <summary>Hex digits of the hash that name a terrain file.</summary>
        public const int HashLength = 16;

        readonly Dictionary<object, int> _blocks;

        internal WrittenTerrain(Dictionary<object, int> blocks, string hash)
        {
            _blocks = blocks;
            Hash = hash;
        }

        /// <summary>16 hex digits of the SHA-256 of the blocks, table and header: the same terrain gets the same hash.</summary>
        public string Hash { get; }

        public int BlockCount => _blocks.Count;

        public int BlockOf(ChunkHeights heights) =>
            heights != null && _blocks.TryGetValue(heights, out var block)
                ? block
                : throw new ArgumentException("These heights were not written to the terrain file.", nameof(heights));

        public int BlockOf(ChunkPaint paint) =>
            paint != null && _blocks.TryGetValue(paint, out var block)
                ? block
                : throw new ArgumentException("This paint was not written to the terrain file.", nameof(paint));
    }
}
