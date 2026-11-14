using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Forge.Data.Plugins;
using Forge.Data.Records;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Packs one block's placed objects into bytes and back. A block holds the objects of one chunk of one space, so
    /// the space is not repeated; namespaces and base object ids go through a string table, since a block repeats
    /// a few of them; names are written once each. Transforms are plain floats. A block where some object wears other
    /// materials (<see cref="PlacedObjectRecord.Materials"/>) is written in <see cref="WithMaterials"/>: each object
    /// then also lists its overrides (slot, material namespace, material name, all through the string table). A block
    /// where some object is flagged (<see cref="PlacedObjectRecord.StartsDead"/>) is written in <see cref="WithFlags"/>:
    /// each object lists its overrides and then its flags, a byte (<see cref="ObjectFlags"/>). Every other block stays in
    /// <see cref="Plain"/> or <see cref="WithMaterials"/>, byte for byte what it always was.
    /// </summary>
    static class ObjectCodec
    {
        /// <summary>The codec of blocks whose objects all wear their base object's materials.</summary>
        public const byte Plain = 1;

        /// <summary>The codec of blocks where some object wears other materials in some of its slots.</summary>
        public const byte WithMaterials = 2;

        /// <summary>The codec of blocks where some object is flagged (placed dead): each lists its materials, then its flags.</summary>
        public const byte WithFlags = 3;

        /// <summary>A placed object's flags, as a <see cref="WithFlags"/> block writes them.</summary>
        [Flags]
        enum ObjectFlags : byte
        {
            None = 0,
            StartsDead = 1,
        }

        static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static bool IsKnown(byte codec) => codec == Plain || codec == WithMaterials || codec == WithFlags;

        /// <param name="codec">Which codec the block was written in, stored beside it.</param>
        public static byte[] Encode(IReadOnlyList<PlacedObjectRecord> objects, out byte codec)
        {
            if (objects == null)
                throw new ArgumentNullException(nameof(objects));

            codec = Plain;
            foreach (var placed in objects)
            {
                if (FlagsOf(placed) != ObjectFlags.None)
                {
                    codec = WithFlags;
                    break;
                }

                if (placed.Materials.Count > 0)
                    codec = WithMaterials;
            }

            var strings = new List<string>();
            var indexOf = new Dictionary<string, int>(StringComparer.Ordinal);
            int Index(string text)
            {
                if (!indexOf.TryGetValue(text, out var index))
                {
                    if (strings.Count == ushort.MaxValue)
                        throw new InvalidOperationException("A block holds more distinct ids than its string table can name.");
                    indexOf.Add(text, index = strings.Count);
                    strings.Add(text);
                }

                return index;
            }

            // Pass one fills the table; the entries are written after it.
            var entries = new MemoryStream();
            using (var writer = new BinaryWriter(entries, Utf8, leaveOpen: true))
            {
                foreach (var placed in objects)
                {
                    writer.Write((ushort)Index(placed.Id.Namespace));
                    writer.Write(placed.Id.Name);
                    writer.Write((ushort)Index(placed.BaseId.Namespace));
                    writer.Write((ushort)Index(placed.BaseId.Name));
                    Write(writer, placed.Position);
                    Write(writer, placed.Rotation);
                    writer.Write(placed.Scale);
                    if (codec == Plain)
                        continue;
                    writer.Write((byte)placed.Materials.Count);
                    foreach (var material in placed.Materials)
                    {
                        writer.Write((ushort)Index(material.Slot));
                        writer.Write((ushort)Index(material.Material.Namespace));
                        writer.Write((ushort)Index(material.Material.Name));
                    }

                    if (codec == WithFlags)
                        writer.Write((byte)FlagsOf(placed));
                }
            }

            using (var block = new MemoryStream())
            using (var writer = new BinaryWriter(block, Utf8, leaveOpen: true))
            {
                writer.Write(objects.Count);
                writer.Write((ushort)strings.Count);
                foreach (var text in strings)
                    writer.Write(text);
                entries.Position = 0;
                entries.CopyTo(block);
                writer.Flush();
                return block.ToArray();
            }
        }

        /// <summary>
        /// The objects of <paramref name="block"/>, all standing in chunk <paramref name="key"/>, each owned by
        /// <paramref name="manifest"/>'s plugin or one of its masters, sorted by id.
        /// </summary>
        /// <exception cref="DataValidationException">The block is damaged or breaks those rules.</exception>
        public static IReadOnlyList<PlacedObjectRecord> Decode(byte[] block, byte codec, ObjectBlockKey key, PluginManifest manifest)
        {
            if (!IsKnown(codec))
                throw Damaged($"is in unknown codec {codec}");

            try
            {
                using (var reader = new BinaryReader(new MemoryStream(block, writable: false), Utf8))
                {
                    var count = reader.ReadInt32();
                    if (count < 0)
                        throw Damaged("has a negative object count");
                    var strings = new string[reader.ReadUInt16()];
                    for (var i = 0; i < strings.Length; i++)
                        strings[i] = reader.ReadString();

                    var objects = new PlacedObjectRecord[count];
                    var seen = new HashSet<RecordId>();
                    for (var i = 0; i < count; i++)
                    {
                        var id = new RecordId(strings[reader.ReadUInt16()], reader.ReadString());
                        var baseId = new RecordId(strings[reader.ReadUInt16()], strings[reader.ReadUInt16()]);
                        var position = ReadVector3(reader);
                        var rotation = ReadQuaternion(reader);
                        var scale = reader.ReadSingle();
                        var materials = codec != Plain ? ReadMaterials(reader, strings) : null;
                        var flags = codec == WithFlags ? (ObjectFlags)reader.ReadByte() : ObjectFlags.None;
                        if ((flags & ~ObjectFlags.StartsDead) != ObjectFlags.None)
                            throw Damaged($"gives '{id}' flags this Forge doesn't know ({(byte)flags})");

                        if (id.Namespace != manifest.Id && !manifest.HasMaster(id.Namespace))
                            throw new DataValidationException(new[] { $"Plugin '{manifest.Id}' writes record '{id}' but '{id.Namespace}' is neither the plugin itself nor one of its masters." });
                        if (!seen.Add(id))
                            throw new DataValidationException(new[] { $"Plugin '{manifest.Id}' contains record '{id}' more than once." });
                        var placed = new PlacedObjectRecord(id, baseId, key.Space, position, rotation, scale, materials,
                            (flags & ObjectFlags.StartsDead) != ObjectFlags.None);
                        if (ObjectBlockKey.Of(placed) != key)
                            throw Damaged($"holds '{id}', which stands in chunk {ObjectBlockKey.Of(placed).Coord}, not {key.Coord}");
                        objects[i] = placed;
                    }

                    if (reader.BaseStream.Position != reader.BaseStream.Length)
                        throw Damaged("holds more than its objects");
                    for (var i = 1; i < objects.Length; i++)
                    {
                        if (objects[i - 1].Id.CompareTo(objects[i].Id) >= 0)
                            throw Damaged("is not sorted by id");
                    }

                    return objects;
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IndexOutOfRangeException || ex is ArgumentException || ex is IOException)
            {
                throw new DataValidationException(new[] { $"An objects block of {key} is damaged: {ex.Message}" }, ex);
            }
        }

        /// <summary>A 64-bit FNV-1a hash of an id, by which a stored block names its objects without their strings.</summary>
        public static ulong HashOf(RecordId id)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            foreach (var c in id.Namespace)
                hash = (hash ^ c) * prime;
            hash = (hash ^ ':') * prime;
            foreach (var c in id.Name)
                hash = (hash ^ c) * prime;
            return hash;
        }

        static ObjectFlags FlagsOf(PlacedObjectRecord placed) => placed.StartsDead ? ObjectFlags.StartsDead : ObjectFlags.None;

        static MaterialOverride[] ReadMaterials(BinaryReader reader, string[] strings)
        {
            var materials = new MaterialOverride[reader.ReadByte()];
            for (var i = 0; i < materials.Length; i++)
            {
                var slot = strings[reader.ReadUInt16()];
                materials[i] = new MaterialOverride(slot, new RecordId(strings[reader.ReadUInt16()], strings[reader.ReadUInt16()]));
            }

            return materials;
        }

        static DataValidationException Damaged(string problem) => new DataValidationException(new[] { $"An objects block {problem}." });

        static void Write(BinaryWriter writer, Vector3 v)
        {
            writer.Write(v.X);
            writer.Write(v.Y);
            writer.Write(v.Z);
        }

        static void Write(BinaryWriter writer, Quaternion q)
        {
            writer.Write(q.X);
            writer.Write(q.Y);
            writer.Write(q.Z);
            writer.Write(q.W);
        }

        static Vector3 ReadVector3(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        static Quaternion ReadQuaternion(BinaryReader reader) => new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
