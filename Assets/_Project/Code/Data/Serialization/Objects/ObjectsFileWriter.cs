using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Forge.Data.Plugins;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Writes a plugin's placed objects as an <see cref="ObjectsFile"/>, one block per chunk. Blocks still stored in
    /// another objects file are copied as packed, without decoding, so saving a large world costs little more than
    /// its changes.
    /// </summary>
    public static class ObjectsFileWriter
    {
        static readonly Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Writes <paramref name="blocks"/> to <paramref name="output"/>, which must be seekable, from its start.</summary>
        public static WrittenObjects Write(Stream output, IObjectBlocks blocks)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (!output.CanSeek || !output.CanWrite)
                throw new ArgumentException("The output must be writable and seekable.", nameof(output));
            if (blocks == null)
                throw new ArgumentNullException(nameof(blocks));

            var stored = blocks as ObjectsFile;
            var spaces = new List<string>();
            var spaceIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var table = new MemoryStream();
            var count = 0;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (var writer = new BinaryWriter(output, Utf8, leaveOpen: true))
            using (var tableWriter = new BinaryWriter(table, Utf8, leaveOpen: true))
            {
                output.Position = 0;
                writer.Write(new byte[ObjectsFile.HeaderSize]);

                var entries = new MemoryStream();
                using (var entryWriter = new BinaryWriter(entries, Utf8, leaveOpen: true))
                {
                    foreach (var key in blocks.Keys.OrderBy(k => k))
                    {
                        byte[] packed;
                        ulong[] hashes;
                        if (stored != null)
                        {
                            packed = stored.ReadRaw(key);
                            hashes = stored.HashesOf(key);
                        }
                        else
                        {
                            var objects = blocks.Read(key);
                            packed = ObjectCodec.Encode(objects);
                            hashes = objects.Select(o => ObjectCodec.HashOf(o.Id)).ToArray();
                        }

                        if (hashes.Length == 0)
                            continue;

                        var space = key.Space.ToString();
                        if (!spaceIndex.TryGetValue(space, out var index))
                        {
                            if (spaces.Count == ushort.MaxValue)
                                throw new InvalidOperationException("More spaces than an objects file can name.");
                            spaceIndex.Add(space, index = spaces.Count);
                            spaces.Add(space);
                        }

                        entryWriter.Write((ushort)index);
                        entryWriter.Write(key.Coord.X);
                        entryWriter.Write(key.Coord.Z);
                        entryWriter.Write(output.Position);
                        entryWriter.Write(packed.Length);
                        entryWriter.Write(hashes.Length);
                        entryWriter.Write(ObjectCodec.Id);
                        entryWriter.Write((byte)0);
                        foreach (var h in hashes)
                            entryWriter.Write(h);
                        writer.Write(packed);
                        hash.AppendData(packed);
                        count++;
                    }
                }

                tableWriter.Write((ushort)spaces.Count);
                foreach (var space in spaces)
                    tableWriter.Write(space);
                entries.Position = 0;
                entries.CopyTo(table);
                var overrides = blocks.Overrides.OrderBy(id => id).ToArray();
                tableWriter.Write(overrides.Length);
                foreach (var id in overrides)
                    tableWriter.Write(id.ToString());
                tableWriter.Flush();

                var tableOffset = output.Position;
                var tableBytes = table.ToArray();
                writer.Write(tableBytes);
                hash.AppendData(tableBytes);

                var header = new MemoryStream(ObjectsFile.HeaderSize);
                using (var headerWriter = new BinaryWriter(header, Utf8, leaveOpen: true))
                {
                    headerWriter.Write(ObjectsFile.Magic);
                    headerWriter.Write(ObjectsFile.FormatVersion);
                    headerWriter.Write((ushort)0);
                    headerWriter.Write(count);
                    headerWriter.Write(tableOffset);
                }

                var headerBytes = header.ToArray();
                output.Position = 0;
                writer.Write(headerBytes);
                hash.AppendData(headerBytes);
                output.Position = output.Length;
                writer.Flush();

                return new WrittenObjects(count, Hex(hash.GetHashAndReset(), WrittenObjects.HashLength));
            }
        }

        static string Hex(byte[] bytes, int length)
        {
            var text = new StringBuilder(length);
            for (var i = 0; text.Length < length; i++)
                text.Append(bytes[i].ToString("x2"));
            return text.ToString(0, length);
        }
    }

    /// <summary>What <see cref="ObjectsFileWriter"/> wrote: how many blocks, and the content's hash.</summary>
    public sealed class WrittenObjects
    {
        /// <summary>Hex digits of the hash that name an objects file.</summary>
        public const int HashLength = 16;

        internal WrittenObjects(int blockCount, string hash)
        {
            BlockCount = blockCount;
            Hash = hash;
        }

        public int BlockCount { get; }

        /// <summary>16 hex digits of the SHA-256 of the blocks, table and header: the same objects get the same hash.</summary>
        public string Hash { get; }
    }

    /// <summary>The objects file a plugin's JSON names (a file name, no folder).</summary>
    public sealed class ObjectsLink
    {
        public ObjectsLink(string fileName)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        }

        public string FileName { get; }
    }
}
