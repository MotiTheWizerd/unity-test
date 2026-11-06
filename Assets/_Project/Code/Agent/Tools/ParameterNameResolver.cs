using System.Reflection;
using Forge.Editing.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Forge.Agent.Tools
{
    /// <summary>
    /// Reads and writes command arguments and results under the names commands give them (snake_case, see
    /// <see cref="CommandParameters.NameOf"/>), makes every argument that isn't optional required, and leaves a
    /// result's pictures out (<see cref="IPictureResult"/>).
    /// </summary>
    sealed class ParameterNameResolver : DefaultContractResolver
    {
        protected override string ResolvePropertyName(string propertyName) => CommandParameters.NameOf(propertyName);

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            // A result's pictures go to the model as pictures, not as base64 inside its JSON.
            if (member.Name == nameof(IPictureResult.Pictures) && typeof(IPictureResult).IsAssignableFrom(member.DeclaringType))
                property.Ignored = true;
            var info = member.GetCustomAttribute<ParamAttribute>();
            if (info != null && !info.Optional)
                property.Required = Required.Always;
            return property;
        }
    }
}
