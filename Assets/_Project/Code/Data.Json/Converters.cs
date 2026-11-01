using System;
using System.Numerics;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Forge.Data.Json
{
    /// <summary>Writes <see cref="RecordId"/> as its canonical "namespace:name" string.</summary>
    sealed class RecordIdConverter : JsonConverter<RecordId>
    {
        public override void WriteJson(JsonWriter writer, RecordId value, JsonSerializer serializer) =>
            writer.WriteValue(value.ToString());

        public override RecordId ReadJson(JsonReader reader, Type objectType, RecordId existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
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
    /// Writes a height field as its resolution plus base64-encoded little-endian
    /// float32 samples.
    /// </summary>
    /// <remarks>
    /// SHORTCUT: heights are embedded in the plugin JSON. Remove when plugins
    /// become packages with binary side files (see ARCHITECTURE.md).
    /// </remarks>
    sealed class HeightFieldConverter : JsonConverter<HeightField>
    {
        const string ResolutionProperty = "resolution";
        const string DataProperty = "data";

        public override void WriteJson(JsonWriter writer, HeightField value, JsonSerializer serializer)
        {
            var samples = value.ToArray();
            var bytes = new byte[samples.Length * sizeof(float)];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            if (!BitConverter.IsLittleEndian)
                ReverseEachFloat(bytes);

            writer.WriteStartObject();
            writer.WritePropertyName(ResolutionProperty);
            writer.WriteValue(value.Resolution);
            writer.WritePropertyName(DataProperty);
            writer.WriteValue(Convert.ToBase64String(bytes));
            writer.WriteEndObject();
        }

        public override HeightField ReadJson(JsonReader reader, Type objectType, HeightField existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var json = JObject.Load(reader);
            var resolution = json.Value<int?>(ResolutionProperty)
                ?? throw new JsonSerializationException($"Missing '{ResolutionProperty}' at {json.Path}.");
            var data = json.Value<string>(DataProperty)
                ?? throw new JsonSerializationException($"Missing '{DataProperty}' at {json.Path}.");

            var bytes = Convert.FromBase64String(data);
            if (bytes.Length % sizeof(float) != 0)
                throw new JsonSerializationException($"Height data at {json.Path} is not a whole number of floats.");
            if (!BitConverter.IsLittleEndian)
                ReverseEachFloat(bytes);

            var samples = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
            return new HeightField(resolution, samples);
        }

        static void ReverseEachFloat(byte[] bytes)
        {
            for (var i = 0; i < bytes.Length; i += sizeof(float))
                Array.Reverse(bytes, i, sizeof(float));
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
