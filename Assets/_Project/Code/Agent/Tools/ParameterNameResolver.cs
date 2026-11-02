using System.Reflection;
using Forge.Editing.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Forge.Agent.Tools
{
    /// <summary>
    /// Reads and writes command arguments and results under the names commands give them (snake_case, see
    /// <see cref="CommandParameters.NameOf"/>), and makes every argument that isn't optional required.
    /// </summary>
    sealed class ParameterNameResolver : DefaultContractResolver
    {
        protected override string ResolvePropertyName(string propertyName) => CommandParameters.NameOf(propertyName);

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            var info = member.GetCustomAttribute<ParamAttribute>();
            if (info != null && !info.Optional)
                property.Required = Required.Always;
            return property;
        }
    }
}
