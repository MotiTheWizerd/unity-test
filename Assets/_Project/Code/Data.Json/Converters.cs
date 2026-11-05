using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.Data.Terrain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Forge.Data.Json
{
    /// <summary>
    /// Writes <see cref="RecordId"/> as its canonical "namespace:name" string; an optional id (a space's climate) is
    /// the same string, or null.
    /// </summary>
    sealed class RecordIdConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(RecordId) || objectType == typeof(RecordId?);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is RecordId id)
                writer.WriteValue(id.ToString());
            else
                writer.WriteNull();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null && objectType == typeof(RecordId?))
                return null;

            var text = reader.Value as string;
            if (reader.TokenType != JsonToken.String || !RecordId.TryParse(text, out var id))
                throw new JsonSerializationException($"Invalid record id '{reader.Value}' at {reader.Path}.");
            return id;
        }
    }

    /// <summary>Writes vectors as compact [x, y, z] arrays.</summary>
    sealed class Vector3Converter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer) =>
            FloatArrays.Write(writer, value.X, value.Y, value.Z);

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var v = FloatArrays.Read(reader, 3);
            return new Vector3(v[0], v[1], v[2]);
        }
    }

    /// <summary>Writes quaternions as compact [x, y, z, w] arrays.</summary>
    sealed class QuaternionConverter : JsonConverter<Quaternion>
    {
        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer) =>
            FloatArrays.Write(writer, value.X, value.Y, value.Z, value.W);

        public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var q = FloatArrays.Read(reader, 4);
            return new Quaternion(q[0], q[1], q[2], q[3]);
        }
    }

    /// <summary>
    /// Polymorphic records via a "type" discriminator resolved only through the
    /// closed <see cref="RecordTypeRegistry"/>. Newtonsoft's TypeNameHandling is
    /// never used: plugin files are untrusted input.
    /// </summary>
    sealed class RecordConverter : JsonConverter
    {
        public const string TypeProperty = "type";
        const string IdProperty = "id";

        readonly RecordTypeRegistry _registry;
        JsonSerializer _bodySerializer;

        public RecordConverter(RecordTypeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        // Newtonsoft picks converters by the runtime type when writing list items,
        // so this must claim every record type, not just the abstract base.
        public override bool CanConvert(Type objectType) => typeof(Record).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var record = (Record)value;
            var body = JObject.FromObject(record, BodySerializer(serializer));
            body.Remove(IdProperty);

            var result = new JObject
            {
                [TypeProperty] = _registry.GetKey(record.GetType()),
                [IdProperty] = record.Id.ToString(),
            };
            result.Merge(body);
            result.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var json = JObject.Load(reader);
            var key = json.Value<string>(TypeProperty);
            if (!_registry.TryGetType(key, out var type))
                throw new JsonSerializationException($"Unknown record type '{key}' at {json.Path}.");

            json.Remove(TypeProperty);
            return json.ToObject(type, BodySerializer(serializer));
        }

        // Same settings and converters as the outer serializer, minus this
        // converter, so record bodies serialize field-by-field without recursion.
        JsonSerializer BodySerializer(JsonSerializer outer)
        {
            if (_bodySerializer != null)
                return _bodySerializer;

            var body = new JsonSerializer
            {
                ContractResolver = outer.ContractResolver,
                TypeNameHandling = outer.TypeNameHandling,
                MetadataPropertyHandling = outer.MetadataPropertyHandling,
                MissingMemberHandling = outer.MissingMemberHandling,
                NullValueHandling = outer.NullValueHandling,
                MaxDepth = outer.MaxDepth,
            };
            foreach (var converter in outer.Converters)
            {
                if (converter != this)
                    body.Converters.Add(converter);
            }

            return _bodySerializer = body;
        }
    }

    /// <summary>Writes chunk coordinates as compact [x, z] arrays.</summary>
    sealed class ChunkCoordConverter : JsonConverter<ChunkCoord>
    {
        public override void WriteJson(JsonWriter writer, ChunkCoord value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteValue(value.X);
            writer.WriteValue(value.Z);
            writer.WriteEndArray();
        }

        public override ChunkCoord ReadJson(JsonReader reader, Type objectType, ChunkCoord existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var array = JArray.Load(reader);
            if (array.Count != 2)
                throw new JsonSerializationException($"Expected [x, z] at {reader.Path}.");
            return new ChunkCoord(array[0].Value<int>(), array[1].Value<int>());
        }
    }

    /// <summary>
    /// Writes a chunk's heights as where they are in the plugin's terrain file: <c>{"resolution": 65, "block": 3}</c>.
    /// Reading gives heights that load from that block when first needed.
    /// </summary>
    sealed class ChunkHeightsConverter : JsonConverter
    {
        const string ResolutionProperty = "resolution";
        const string BlockProperty = "block";

        readonly ITerrainBlockWriter _written;
        readonly ITerrainBlockReader _stored;

        /// <param name="written">Writing: where each chunk's heights went; null when the plugin has no terrain.</param>
        public ChunkHeightsConverter(ITerrainBlockWriter written)
        {
            _written = written;
        }

        /// <param name="stored">Reading: the terrain file the plugin names; null when it names none.</param>
        public ChunkHeightsConverter(ITerrainBlockReader stored)
        {
            _stored = stored;
        }

        public override bool CanConvert(Type objectType) => typeof(ChunkHeights).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var heights = (ChunkHeights)value;
            if (_written == null)
                throw new JsonSerializationException("Chunk heights can only be written alongside a terrain file.");

            writer.WriteStartObject();
            writer.WritePropertyName(ResolutionProperty);
            writer.WriteValue(heights.Resolution);
            writer.WritePropertyName(BlockProperty);
            writer.WriteValue(_written.BlockOf(heights));
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var json = JObject.Load(reader);
            if (json.Properties().Any(p => p.Name != ResolutionProperty && p.Name != BlockProperty))
                throw new JsonSerializationException($"Chunk heights at {json.Path} hold only '{ResolutionProperty}' and '{BlockProperty}'.");
            var resolution = json.Value<int?>(ResolutionProperty)
                ?? throw new JsonSerializationException($"Missing '{ResolutionProperty}' at {json.Path}.");
            var block = json.Value<int?>(BlockProperty)
                ?? throw new JsonSerializationException($"Missing '{BlockProperty}' at {json.Path}.");
            if (_stored == null)
                throw new JsonSerializationException($"The chunk at {json.Path} refers to terrain block {block}, but the plugin names no terrain file.");

            return _stored.Block(block, resolution);
        }
    }

    /// <summary>
    /// Writes a chunk's ground paint as the layers it names and where their weights are in the plugin's terrain file:
    /// <c>{"layers": ["forge.core:ground/grass", …], "block": 7}</c>. Reading gives paint that loads from that block
    /// when first needed.
    /// </summary>
    sealed class ChunkPaintConverter : JsonConverter
    {
        const string LayersProperty = "layers";
        const string BlockProperty = "block";

        readonly ITerrainBlockWriter _written;
        readonly ITerrainBlockReader _stored;

        /// <param name="written">Writing: where each chunk's paint went; null when the plugin has no terrain.</param>
        public ChunkPaintConverter(ITerrainBlockWriter written)
        {
            _written = written;
        }

        /// <param name="stored">Reading: the terrain file the plugin names; null when it names none.</param>
        public ChunkPaintConverter(ITerrainBlockReader stored)
        {
            _stored = stored;
        }

        public override bool CanConvert(Type objectType) => typeof(ChunkPaint).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var paint = (ChunkPaint)value;
            if (_written == null)
                throw new JsonSerializationException("Chunk paint can only be written alongside a terrain file.");

            writer.WriteStartObject();
            writer.WritePropertyName(LayersProperty);
            serializer.Serialize(writer, paint.Layers);
            writer.WritePropertyName(BlockProperty);
            writer.WriteValue(_written.BlockOf(paint));
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var json = JObject.Load(reader);
            if (json.Properties().Any(p => p.Name != LayersProperty && p.Name != BlockProperty))
                throw new JsonSerializationException($"Chunk paint at {json.Path} holds only '{LayersProperty}' and '{BlockProperty}'.");
            var layers = (json[LayersProperty] ?? throw new JsonSerializationException($"Missing '{LayersProperty}' at {json.Path}."))
                .ToObject<List<RecordId>>(serializer);
            var block = json.Value<int?>(BlockProperty)
                ?? throw new JsonSerializationException($"Missing '{BlockProperty}' at {json.Path}.");
            if (_stored == null)
                throw new JsonSerializationException($"The chunk at {json.Path} refers to paint block {block}, but the plugin names no terrain file.");

            return _stored.Paint(block, layers);
        }
    }

    static class FloatArrays
    {
        public static void Write(JsonWriter writer, params float[] values)
        {
            writer.WriteStartArray();
            foreach (var value in values)
                writer.WriteValue(value);
            writer.WriteEndArray();
        }

        public static float[] Read(JsonReader reader, int length)
        {
            var array = JArray.Load(reader);
            if (array.Count != length)
                throw new JsonSerializationException($"Expected {length} numbers at {reader.Path} but found {array.Count}.");
            return array.ToObject<float[]>();
        }
    }
}
