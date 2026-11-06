using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.Memory
{
    /// <summary>The Semantix server's JSON doors, as the signed-in user.</summary>
    public interface ISemantixHttp
    {
        /// <summary>GET <paramref name="path"/> ("/api/v1/..."); throws <see cref="SemantixException"/> on failure.</summary>
        Task<JToken> GetAsync(string path, TimeSpan timeout, CancellationToken cancellation);

        /// <summary>POST <paramref name="body"/> to <paramref name="path"/>; throws <see cref="SemantixException"/> on failure.</summary>
        Task<JToken> PostAsync(string path, JObject body, TimeSpan timeout, CancellationToken cancellation);
    }

    /// <summary>The Semantix server could not be reached, or refused; the message says why, in its own words when it gave some.</summary>
    public sealed class SemantixException : Exception
    {
        public SemantixException(string message, int status = 0, Exception inner = null) : base(message, inner) => Status = status;

        /// <summary>The HTTP status it answered with, or 0 when it didn't answer.</summary>
        public int Status { get; }
    }
}
