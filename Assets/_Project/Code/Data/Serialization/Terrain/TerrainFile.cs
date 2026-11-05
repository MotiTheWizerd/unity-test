using System;
using System.IO;
using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// A plugin's terrain file (<c>{plugin}.{hash}.terrain</c>), opened for reading: the chunk heights its JSON
    /// refers to by block number. Opening reads only the header and the block table; each block is read and decoded
    /// when its chunk's heights are first needed (<see cref="ChunkHeights.Load"/>) and kept in the shared
    /// <see cref="HeightCache"/>. The file is not held open, so it can be replaced or removed while in use.
    /// </summary>
    /// <remarks>
    /// Layout, little-endian. A header: "FTRN", format version (u16), reserved (u16), meters per height step (f32),
    /// block count (i32), table offset (i64). The blocks follow, then the table: per block its offset (i64),
    /// length (i32), resolution (u16), codec (u8) and a reserved byte. The table comes last so the file is written in
    /// one pass.
    /// </remarks>
    public sealed class TerrainFile : ITerrainBlockReader
    {
        public const string Extension = ".terrain";

        internal static readonly byte[] Magic = { (byte)'F', (byte)'T', (byte)'R', (byte)'N' };
        internal const ushort FormatVersion = 1;
        internal const int HeaderSize = 24;
        internal const int EntrySize = 16;

        readonly Entry[] _entries;
        readonly StoredHeights[] _handles;
        readonly HeightCache _cache;

        TerrainFile(string path, float metersPerStep, Entry[] entries, HeightCache cache)
        {
            Path = path;
            MetersPerStep = metersPerStep;
            _entries = entries;
            _handles = new StoredHeights[entries.Length];
            _cache = cache;
        }

        public string Path { get; }

        /// <summary>The height one 16-bit step stands for, in meters.</summary>
        public float MetersPerStep { get; }

        public int BlockCount => _entries.Length;

        /// <summary>Opens the file at <paramref name="path"/>, reading its header and block table only.</summary>
        /// <exception cref="DataValidationException">The file is missing, cut short or not a terrain file.</exception>
        public static TerrainFile Open(string path, HeightCache cache)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A path is required.", nameof(path));
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            var name = System.IO.Path.GetFileName(path);
            try
            {
                using (var stream = OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    var magic = reader.ReadBytes(Magic.Length);
                    if (magic.Length != Magic.Length || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
                        throw Invalid(name, "is not a terrain file");
                    var version = reader.ReadUInt16();
                    if (version != FormatVersion)
                        throw Invalid(name, $"has terrain format {version}; expected {FormatVersion}");
                    reader.ReadUInt16();
                    var step = reader.ReadSingle();
                    if (!(step > 0f && step <= 1f))
                        throw Invalid(name, $"has height steps of {step} m, outside (0, 1]");
                    var count = reader.ReadInt32();
                    var tableOffset = reader.ReadInt64();
                    if (count < 0 || tableOffset < HeaderSize || tableOffset + (long)count * EntrySize != stream.Length)
                        throw Invalid(name, "has a damaged block table");

                    stream.Position = tableOffset;
                    var entries = new Entry[count];
                    for (var i = 0; i < count; i++)
                    {
                        var entry = new Entry(reader.ReadInt64(), reader.ReadInt32(), reader.ReadUInt16(), reader.ReadByte());
                        reader.ReadByte();
                        if (entry.Offset < HeaderSize || entry.Length <= 0 || entry.Offset + entry.Length > tableOffset)
                            throw Invalid(name, $"has block {i} outside its data");
                        if (!WorldGrid.IsValidHeightmapResolution(entry.Resolution))
                            throw Invalid(name, $"has block {i} at resolution {entry.Resolution}");
                        if (entry.Codec != HeightCodec.Id)
                            throw Invalid(name, $"has block {i} in unknown codec {entry.Codec}");
                        entries[i] = entry;
                    }

                    return new TerrainFile(path, step, entries, cache);
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                throw new DataValidationException(new[] { $"Terrain file {name} is missing." }, ex);
            }
            catch (EndOfStreamException ex)
            {
                throw new DataValidationException(new[] { $"Terrain file {name} is cut short." }, ex);
            }
        }

        public ChunkHeights Block(int index, int resolution)
        {
            if (index < 0 || index >= _entries.Length)
                throw Invalid(System.IO.Path.GetFileName(Path), $"has no block {index}; it has {_entries.Length}");
            if (_entries[index].Resolution != resolution)
                throw Invalid(System.IO.Path.GetFileName(Path), $"has block {index} at resolution {_entries[index].Resolution}, not {resolution}");
            // One handle per block, so chunks sharing a block share its cached heights too.
            return _handles[index] ?? (_handles[index] = new StoredHeights(this, index, resolution));
        }

        /// <summary>The decoded heights of <paramref name="heights"/>, from the cache or the disk.</summary>
        internal HeightField Load(StoredHeights heights) =>
            _cache.GetOrLoad(heights, () => HeightCodec.Decode(ReadRaw(heights.Index), heights.Resolution, MetersPerStep));

        /// <summary>Block <paramref name="index"/> as stored, still packed: copied as is when the terrain is saved again.</summary>
        internal byte[] ReadRaw(int index)
        {
            var entry = _entries[index];
            try
            {
                using (var stream = OpenRead(Path))
                {
                    stream.Position = entry.Offset;
                    var block = new byte[entry.Length];
                    var read = 0;
                    while (read < block.Length)
                    {
                        var n = stream.Read(block, read, block.Length - read);
                        if (n == 0)
                            throw new EndOfStreamException();
                        read += n;
                    }

                    return block;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new DataValidationException(new[] { $"Terrain file {System.IO.Path.GetFileName(Path)} can't be read: {ex.Message}" }, ex);
            }
        }

        // Others may read it too, and saving may remove an older file while it is open here.
        static FileStream OpenRead(string path) =>
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

        static DataValidationException Invalid(string name, string problem) =>
            new DataValidationException(new[] { $"Terrain file {name} {problem}." });

        readonly struct Entry
        {
            public Entry(long offset, int length, ushort resolution, byte codec)
            {
                Offset = offset;
                Length = length;
                Resolution = resolution;
                Codec = codec;
            }

            public long Offset { get; }

            public int Length { get; }

            public ushort Resolution { get; }

            public byte Codec { get; }
        }
    }

    /// <summary>Chunk heights stored in a block of a <see cref="TerrainFile"/>, read when first needed.</summary>
    sealed class StoredHeights : ChunkHeights
    {
        public StoredHeights(TerrainFile file, int index, int resolution)
            : base(resolution)
        {
            File = file;
            Index = index;
        }

        public TerrainFile File { get; }

        public int Index { get; }

        public override HeightField Load() => File.Load(this);
    }
}
