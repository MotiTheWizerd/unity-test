using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// A plugin's terrain file (<c>{plugin}.{hash}.terrain</c>), opened for reading: the chunk heights and ground paint
    /// its JSON refers to by block number. Opening reads only the header and the block table; each block is read and
    /// decoded when its chunk's heights or paint are first needed (<see cref="ChunkHeights.Load"/>,
    /// <see cref="ChunkPaint.Load"/>) and kept in the shared <see cref="TerrainCache"/>. The file is not held open, so it
    /// can be replaced or removed while in use.
    /// </summary>
    /// <remarks>
    /// Layout, little-endian. A header: "FTRN", format version (u16), reserved (u16), meters per height step (f32),
    /// block count (i32), table offset (i64). The blocks follow, then the table: per block its offset (i64),
    /// length (i32), resolution (u16), codec (u8) and a reserved byte. The codec tells a block of heights
    /// (<see cref="HeightCodec"/>, resolution = samples per edge) from one of paint (<see cref="PaintCodec"/>,
    /// resolution = texels per edge). The table comes last so the file is written in one pass.
    /// </remarks>
    public sealed class TerrainFile : ITerrainBlockReader
    {
        public const string Extension = ".terrain";

        internal static readonly byte[] Magic = { (byte)'F', (byte)'T', (byte)'R', (byte)'N' };
        internal const ushort FormatVersion = 1;
        internal const int HeaderSize = 24;
        internal const int EntrySize = 16;

        readonly Entry[] _entries;
        // One handle per block (StoredHeights or StoredPaint), so chunks sharing a block share its cached value too.
        readonly object[] _handles;
        readonly TerrainCache _cache;

        TerrainFile(string path, float metersPerStep, Entry[] entries, TerrainCache cache)
        {
            Path = path;
            MetersPerStep = metersPerStep;
            _entries = entries;
            _handles = new object[entries.Length];
            _cache = cache;
        }

        public string Path { get; }

        /// <summary>The height one 16-bit step stands for, in meters.</summary>
        public float MetersPerStep { get; }

        public int BlockCount => _entries.Length;

        /// <summary>Opens the file at <paramref name="path"/>, reading its header and block table only.</summary>
        /// <exception cref="DataValidationException">The file is missing, cut short or not a terrain file.</exception>
        public static TerrainFile Open(string path, TerrainCache cache)
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
                        if (entry.Codec == HeightCodec.Id && !WorldGrid.IsValidHeightmapResolution(entry.Resolution))
                            throw Invalid(name, $"has heights block {i} at resolution {entry.Resolution}");
                        if (entry.Codec == PaintCodec.Id && entry.Resolution != GroundPaint.Resolution)
                            throw Invalid(name, $"has paint block {i} at resolution {entry.Resolution}; expected {GroundPaint.Resolution}");
                        if (entry.Codec != HeightCodec.Id && entry.Codec != PaintCodec.Id)
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
            CheckBlock(index, HeightCodec.Id, "heights");
            if (_entries[index].Resolution != resolution)
                throw Invalid(FileName, $"has block {index} at resolution {_entries[index].Resolution}, not {resolution}");
            return (StoredHeights)(_handles[index] ?? (_handles[index] = new StoredHeights(this, index, resolution)));
        }

        public ChunkPaint Paint(int index, IReadOnlyList<RecordId> layers)
        {
            CheckBlock(index, PaintCodec.Id, "paint");
            if (_handles[index] is StoredPaint shared)
            {
                // Chunks share a block only when they share the paint, layers included.
                if (!shared.Layers.SequenceEqual(layers ?? Array.Empty<RecordId>()))
                    throw Invalid(FileName, $"has paint block {index} named with different layers by two chunks");
                return shared;
            }

            var paint = new StoredPaint(this, index, layers);
            _handles[index] = paint;
            return paint;
        }

        /// <summary>The decoded heights of <paramref name="heights"/>, from the cache or the disk.</summary>
        internal HeightField Load(StoredHeights heights) =>
            _cache.Heights(heights, () => HeightCodec.Decode(ReadRaw(heights.Index), heights.Resolution, MetersPerStep));

        /// <summary>The decoded weights of <paramref name="paint"/>, from the cache or the disk.</summary>
        internal GroundPaint Load(StoredPaint paint) =>
            _cache.Paint(paint, () => PaintCodec.Decode(ReadRaw(paint.Index), paint.Layers));

        string FileName => System.IO.Path.GetFileName(Path);

        void CheckBlock(int index, byte codec, string what)
        {
            if (index < 0 || index >= _entries.Length)
                throw Invalid(FileName, $"has no block {index}; it has {_entries.Length}");
            if (_entries[index].Codec != codec)
                throw Invalid(FileName, $"has no {what} in block {index}");
        }

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

    /// <summary>Chunk paint stored in a block of a <see cref="TerrainFile"/>, read when first needed.</summary>
    sealed class StoredPaint : ChunkPaint
    {
        public StoredPaint(TerrainFile file, int index, IReadOnlyList<RecordId> layers)
            : base(layers)
        {
            File = file;
            Index = index;
        }

        public TerrainFile File { get; }

        public int Index { get; }

        public override GroundPaint Load() => File.Load(this);
    }
}
