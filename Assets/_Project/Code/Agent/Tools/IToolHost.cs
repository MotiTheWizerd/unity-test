using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.Tools
{
    /// <summary>Forge's tools as an agent backend reaches them, whatever the transport or model provider.</summary>
    public interface IToolHost
    {
        IReadOnlyList<ToolDefinition> Tools { get; }

        /// <summary>Runs tool <paramref name="name"/> with JSON <paramref name="arguments"/>; callable from any thread.</summary>
        Task<ToolResult> CallAsync(string name, JObject arguments);
    }
}
