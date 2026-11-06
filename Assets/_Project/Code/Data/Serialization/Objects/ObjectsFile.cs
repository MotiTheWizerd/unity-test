using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// A plugin's objects file (<c>{plugin}.{hash}.objects</c>), opened for reading: its placed objects, a block per
    /// chunk. Opening reads only the header and the table (which block is where, how many objects it holds, the
    /// hashes of their ids, and the ids that override other plugins' objects); a block is read and decoded when its
    /// chunk is first needed and kept in the shared <see cref="ObjectCache"/>. The file is not held open, so it can
    /// be replaced or removed while in use.
    /// </summary>
    /// <remarks>
    /// Layout, little-endian. A header: "FOBJ", format version (u16), reserved (u16), block count (i32), table
    /// offset (i64). The blocks follow, then the table: the space ids as a string table; per block its space (u16),
    /// chunk x and z (i32), offset (i64), length (i32), object count (i32), codec (u8), a reserved byte, then the
    /// 64-bit hash of each object's id; then the override ids as strings. The table comes last so the file is written
    /// in one pass.
    /// </remarks>
    public sealed class ObjectsFile : IObjectBlocks
    {
        public const string Extension = ".objects";

        internal static readonly byte[] Magic = { (byte)'F', (byte)'O', (byte)'B', (byte)'J' };
        internal const ushort FormatVersion = 1;
        internal const int HeaderSize = 20;

        static readonly Encoding Utf8 = new UTF8Encoding(false);
        static readonly IReadOnlyList<PlacedObjectRecord> None = Array.Empty<PlacedObjectRecord>();

        readonly PluginManifest _manifest;
        readonly ObjectCache _cache;
        readonly Entry[] _entries;
        readonly ObjectBlockKey[] _keys;
        readonly Dictionary<ObjectBlockKey, int> _index;
        readonly Dictionary<ulong, int> _locator;
        readonly RecordId[] _overrides;
        readonly BlockHandle[] _handles;

        ObjectsFile(string path, PluginManifest manifest, ObjectCache cache, Entry[] entries, RecordId[] overrides)
        {
            Path = path;
            _manifest = manifest;
            _cache = cache;
            _entries = entries;
            _keys = entries.Select(e => e.Key).ToArray();
            _index = new Dictionary<ObjectBlockKey, int>(entries.Length);
            _locator = new Dictionary<ulong, int>(entries.Sum(e => e.Hashes.Length));
            for (var i = 0; i < entries.Length; i++)
            {
                _index.Add(entries[i].Key, i);
                foreach (var hash in entries[i].Hashes)
                {
                    // The first block to name a hash keeps it; two ids with one hash is a vanishingly rare thing.
                    if (!_locator.ContainsKey(hash))
                        _locator.Add(hash, i);
                }
            }

            _overrides = overrides;
            _handles = new BlockHandle[entries.Length];
            TotalCount = entries.Sum(e => e.Count);
        }

        public string Path { get; }

        public IReadOnlyList<ObjectBlockKey> Keys => _keys;

        public int TotalCount { get; }

        public IReadOnlyCollection<RecordId> Overrides => _overrides;

        /// <summary>Opens the file at <paramref name="path"/>, reading its header and table only.</summary>
        /// <param name="manifest">The plugin the file belongs to; its blocks may only hold its own and its masters' objects.</param>
        /// <exception cref="DataValidationException">The file is missing, cut short or not an objects file.</exception>
        public static ObjectsFile Open(string path, PluginManifest manifest, ObjectCache cache)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A path is required.", nameof(path));
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            var name = System.IO.Path.GetFileName(path);
            try
            {
                using (var stream = OpenRead(path))
                using (var reader = new BinaryReader(stream, Utf8))
                {
                    var magic = reader.ReadBytes(Magic.Length);
                    if (magic.Length != Magic.Length || !magic.SequenceEqual(Magic))
                        throw Invalid(name, "is not an objects file");
                    var version = reader.ReadUInt16();
                    if (version != FormatVersion)
                        throw Invalid(name, $"has objects format {version}; expected {FormatVersion}");
                    reader.ReadUInt16();
                    var count = reader.ReadInt32();
                    var tableOffset = reader.ReadInt64();
                    if (count < 0 || tableOffset < HeaderSize || tableOffset > stream.Length)
                        throw Invalid(name, "has a damaged table");

                    stream.Position = tableOffset;
                    var spaces = new RecordId[reader.ReadUInt16()];
                    for (var i = 0; i < spaces.Length; i++)
                        spaces[i] = ParseId(name, reader.ReadString());

                    var entries = new Entry[count];
                    for (var i = 0; i < count; i++)
                    {
                        var space = spaces[reader.ReadUInt16()];
                        var coord = new ChunkCoord(reader.ReadInt32(), reader.ReadInt32());
                        var offset = reader.ReadInt64();
                        var length = reader.ReadInt32();
                        var objects = reader.ReadInt32();
                        var codec = reader.ReadByte();
                        reader.ReadByte();
                        if (offset < HeaderSize || length < 0 || objects < 0 || offset + length > tableOffset)
                            throw Invalid(name, $"has block {i} outside its data");
                        if (!ObjectCodec.IsKnown(codec))
                            throw Invalid(name, $"has block {i} in unknown codec {codec}");
                        var hashes = new ulong[objects];
                        for (var h = 0; h < objects; h++)
                            hashes[h] = reader.ReadUInt64();
                        var key = new ObjectBlockKey(space, coord);
                        if (i > 0 && entries[i - 1].Key.CompareTo(key) >= 0)
                            throw Invalid(name, "has blocks out of order");
                        entries[i] = new Entry(key, offset, length, objects, codec, hashes);
                    }

                    var overrides = new RecordId[reader.ReadInt32()];
                    for (var i = 0; i < overrides.Length; i++)
                        overrides[i] = ParseId(name, reader.ReadString());
                    if (stream.Position != stream.Length)
                        throw Invalid(name, "has data after its table");

                    return new ObjectsFile(path, manifest, cache, entries, overrides);
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                throw new DataValidationException(new[] { $"Objects file {name} is missing." }, ex);
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IndexOutOfRangeException || ex is ArgumentException)
            {
                throw new DataValidationException(new[] { $"Objects file {name} is cut short or damaged: {ex.Message}" }, ex);
            }
        }

        public int CountOf(ObjectBlockKey key) => _index.TryGetValue(key, out var i) ? _entries[i].Count : 0;

        public IReadOnlyList<PlacedObjectRecord> Read(ObjectBlockKey key)
        {
            if (!_index.TryGetValue(key, out var i))
                return None;
            var handle = _handles[i] ?? (_handles[i] = new BlockHandle(this, i));
            return _cache.GetOrLoad(handle, () => ObjectCodec.Decode(ReadRaw(i), _entries[i].Codec, key, _manifest));
        }

        public bool TryLocate(RecordId id, out ObjectBlockKey key)
        {
            if (_locator.TryGetValue(ObjectCodec.HashOf(id), out var i))
            {
                key = _entries[i].Key;
                return true;
            }

            key = default;
            return false;
        }

        /// <summary>The id hashes of block <paramref name="key"/>, as stored: copied when the file is saved again.</summary>
        internal ulong[] HashesOf(ObjectBlockKey key) => _entries[_index[key]].Hashes;

        /// <summary>The codec block <paramref name="key"/> is stored in, copied with it when the file is saved again.</summary>
        internal byte CodecOf(ObjectBlockKey key) => _entries[_index[key]].Codec;

        /// <summary>Block <paramref name="key"/> as stored, still packed: copied as is when the file is saved again.</summary>
        internal byte[] ReadRaw(ObjectBlockKey key) => ReadRaw(_index[key]);

        byte[] ReadRaw(int index)
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
                throw new DataValidationException(new[] { $"Objects file {System.IO.Path.GetFileName(Path)} can't be read: {ex.Message}" }, ex);
            }
        }

        static RecordId ParseId(string name, string text) =>
            RecordId.TryParse(text, out var id) ? id : throw Invalid(name, $"names an invalid id '{text}'");

        // Others may read it too, and saving may remove an older file while it is open here.
        static FileStream OpenRead(string path) =>
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

        static DataValidationException Invalid(string name, string problem) =>
            new DataValidationException(new[] { $"Objects file {name} {problem}." });

        internal readonly struct Entry
        {
            public Entry(ObjectBlockKey key, long offset, int length, int count, byte codec, ulong[] hashes)
            {
                Key = key;
                Offset = offset;
                Length = length;
                Count = count;
                Codec = codec;
                Hashes = hashes;
            }

            public ObjectBlockKey Key { get; }

            public long Offset { get; }

            public int Length { get; }

            public int Count { get; }

            public byte Codec { get; }

            public ulong[] Hashes { get; }
        }

        // One handle per block: the cache's key, so a block decoded once is shared by every reader of it.
        sealed class BlockHandle
        {
            public BlockHandle(ObjectsFile file, int index)
            {
                File = file;
                Index = index;
            }

            public ObjectsFile File { get; }

            public int Index { get; }
        }
    }
}
