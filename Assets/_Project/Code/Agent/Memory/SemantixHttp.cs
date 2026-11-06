using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.Memory
{
    /// <summary>
    /// <see cref="ISemantixHttp"/> over HTTP. The token is read for every request, so signing in to Semantix while
    /// the editor runs takes effect on the next message.
    /// </summary>
    public sealed class SemantixHttp : ISemantixHttp, IDisposable
    {
        const string ApiUrlVariable = "SEMANTIX_API_URL";

        readonly HttpClient _client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        readonly string _server;
        readonly Func<string> _token;

        /// <param name="server">The server's origin, e.g. https://api.semantixstudio.com.</param>
        /// <param name="token">Reads the bearer token; throws <see cref="SemantixException"/> when there is none.</param>
        public SemantixHttp(string server, Func<string> token)
        {
            if (string.IsNullOrWhiteSpace(server))
                throw new ArgumentException("The Semantix server is required.", nameof(server));
            _server = server.Trim().TrimEnd('/');
            _token = token ?? throw new ArgumentNullException(nameof(token));
        }

        /// <summary>The server to use: <c>SEMANTIX_API_URL</c> when set (as the other Semantix tools read it), else <paramref name="configured"/>.</summary>
        public static string Server(string configured)
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(ApiUrlVariable);
            return string.IsNullOrWhiteSpace(fromEnvironment) ? configured : fromEnvironment;
        }

        public Task<JToken> GetAsync(string path, TimeSpan timeout, CancellationToken cancellation) =>
            SendAsync(HttpMethod.Get, path, null, timeout, cancellation);

        public Task<JToken> PostAsync(string path, JObject body, TimeSpan timeout, CancellationToken cancellation) =>
            SendAsync(HttpMethod.Post, path, body ?? throw new ArgumentNullException(nameof(body)), timeout, cancellation);

        public void Dispose() => _client.Dispose();

        async Task<JToken> SendAsync(HttpMethod method, string path, JObject body, TimeSpan timeout, CancellationToken cancellation)
        {
            using (var request = new HttpRequestMessage(method, _server + path))
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token());
                if (body != null)
                    request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                deadline.CancelAfter(timeout);

                HttpResponseMessage response;
                string text;
                try
                {
                    response = await _client.SendAsync(request, deadline.Token).ConfigureAwait(false);
                    text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                {
                    throw new SemantixException($"Semantix didn't answer within {timeout.TotalSeconds:0} s");
                }
                catch (HttpRequestException ex)
                {
                    throw new SemantixException($"couldn't reach Semantix at {_server} ({ex.GetBaseException().Message})", 0, ex);
                }

                using (response)
                {
                    var status = (int)response.StatusCode;
                    if (status < 200 || status >= 300)
                        throw new SemantixException($"Semantix refused (HTTP {status}): {Detail(text)}", status);
                    try
                    {
                        return string.IsNullOrWhiteSpace(text) ? JValue.CreateNull() : JToken.Parse(text);
                    }
                    catch (JsonException ex)
                    {
                        throw new SemantixException($"Semantix answered something that isn't JSON ({ex.Message})", status, ex);
                    }
                }
            }
        }

        // The server's own words: FastAPI's {"detail": "..."} or {"detail": {"message": "..."}}.
        static string Detail(string text)
        {
            try
            {
                var detail = JToken.Parse(text)["detail"];
                if (detail is JObject withMessage && withMessage["message"] != null)
                    return (string)withMessage["message"];
                if (detail != null)
                    return detail.Type == JTokenType.String ? (string)detail : detail.ToString(Formatting.None);
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
            {
                // Not JSON (a proxy's error page): the text itself says what there is to say.
            }

            text = text?.Trim() ?? string.Empty;
            return text.Length <= 300 ? text : text.Substring(0, 300) + "…";
        }
    }
}
