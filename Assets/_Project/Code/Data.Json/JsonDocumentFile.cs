using System;
using System.IO;
using Forge.Data.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Forge.Data.Json
{
    /// <summary>
    /// One small JSON document kept in a file, such as the editor's preferences: plain C# properties in camelCase,
    /// nulls left out, written atomically (<see cref="AtomicFile"/>). Unlike plugins it is forgiving: a missing file
    /// is the default document, unknown fields (written by a newer build) are skipped, and an unreadable file is
    /// reported and replaced by the default, because losing a preference must never stop the editor from opening.
    /// </summary>
    public sealed class JsonDocumentFile<T> where T : class, new()
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DocumentContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            Formatting = Formatting.Indented,
        };

        string _written;

        public JsonDocumentFile(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public string Path { get; }

        /// <summary>
        /// The document in the file, or a new default one when there is no file or it can't be read; then
        /// <paramref name="problem"/> says why (null when the file was read, or simply wasn't there yet).
        /// </summary>
        public T Load(out string problem)
        {
            problem = null;
            if (!File.Exists(Path))
                return new T();

            try
            {
                var text = File.ReadAllText(Path, AtomicFile.Utf8NoBom);
                var document = JsonConvert.DeserializeObject<T>(text, Settings);
                if (document == null)
                {
                    problem = $"{Path} is empty; starting from the defaults.";
                    return new T();
                }

                // An unchanged document needn't be written again.
                _written = ToText(document);
                return document;
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                problem = $"{Path} could not be read ({ex.Message}); starting from the defaults.";
                return new T();
            }
        }

        /// <summary>Writes <paramref name="document"/> unless the file already holds exactly it; true when it wrote.</summary>
        public bool Save(T document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var text = ToText(document);
            if (text == _written)
                return false;

            AtomicFile.WriteText(Path, writer => writer.Write(text));
            _written = text;
            return true;
        }

        /// <summary>The document as it is written to the file.</summary>
        public static string ToText(T document) => JsonConvert.SerializeObject(document, Settings);
    }

    // A document is its settable properties: read-only ones (helpers computed from the rest) are never written.
    // Dictionary keys are data (record ids), so only the property names are camel-cased.
    sealed class DocumentContractResolver : DefaultContractResolver
    {
        public DocumentContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false };
        }

        protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            if (!property.Writable)
                property.ShouldSerialize = _ => false;
            return property;
        }
    }
}
