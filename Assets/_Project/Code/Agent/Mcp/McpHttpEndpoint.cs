using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Forge.Agent.Mcp
{
    /// <summary>
    /// Serves an <see cref="McpServer"/> over MCP's Streamable HTTP transport, on the loopback interface only, at a
    /// free port and behind a fresh bearer token, so only the agent process it is handed to can call the tools.
    /// Every answer is plain JSON; the server never pushes messages, so there is no event stream.
    /// </summary>
    public sealed class McpHttpEndpoint : IDisposable
    {
        const string PathName = "/mcp";

        static readonly Encoding Utf8 = new UTF8Encoding(false);

        readonly McpServer _server;
        readonly HttpListener _listener = new HttpListener();
        readonly byte[] _expectedAuthorization;
        volatile bool _disposed;

        public McpHttpEndpoint(McpServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            Port = FreeLoopbackPort();
            Token = NewToken();
            _expectedAuthorization = Utf8.GetBytes("Bearer " + Token);
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _ = AcceptAsync();
        }

        /// <summary>Raised, on a worker thread, when serving a request fails for a reason other than a dropped connection.</summary>
        public event Action<Exception> Failed;

        public int Port { get; }

        /// <summary>The bearer token every request must carry.</summary>
        public string Token { get; }

        public string Url => $"http://127.0.0.1:{Port}{PathName}";

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _listener.Close();
        }

        async Task AcceptAsync()
        {
            while (!_disposed)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (_disposed && (ex is HttpListenerException || ex is ObjectDisposedException || ex is InvalidOperationException))
                {
                    return;
                }
                catch (Exception ex)
                {
                    // The listener broke; agents see the tools as unreachable from now on.
                    Failed?.Invoke(ex);
                    return;
                }

                _ = ServeAsync(context);
            }
        }

        async Task ServeAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            try
            {
                if (request.Url.AbsolutePath != PathName)
                {
                    Finish(response, HttpStatusCode.NotFound);
                }
                // Browsers send an Origin; the agent doesn't. Refusing them keeps web pages from reaching the tools.
                else if (!string.IsNullOrEmpty(request.Headers["Origin"]))
                {
                    Finish(response, HttpStatusCode.Forbidden);
                }
                else if (!IsAuthorized(request.Headers["Authorization"]))
                {
                    Finish(response, HttpStatusCode.Unauthorized);
                }
                else if (request.HttpMethod != "POST")
                {
                    response.AddHeader("Allow", "POST");
                    Finish(response, HttpStatusCode.MethodNotAllowed);
                }
                else
                {
                    string body;
                    using (var reader = new StreamReader(request.InputStream, Utf8))
                        body = await reader.ReadToEndAsync().ConfigureAwait(false);

                    var answer = await _server.HandleAsync(body).ConfigureAwait(false);
                    if (answer == null)
                    {
                        Finish(response, HttpStatusCode.Accepted);
                    }
                    else
                    {
                        var bytes = Utf8.GetBytes(answer);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.ContentType = "application/json";
                        response.ContentLength64 = bytes.Length;
                        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                        response.Close();
                    }
                }
            }
            catch (Exception ex) when (ex is HttpListenerException || ex is IOException || ex is ObjectDisposedException)
            {
                // The client hung up or the endpoint closed mid-request; nobody is left to answer.
                response.Abort();
            }
            catch (Exception ex)
            {
                response.Abort();
                Failed?.Invoke(ex);
            }
        }

        static void Finish(HttpListenerResponse response, HttpStatusCode status)
        {
            response.StatusCode = (int)status;
            response.Close();
        }

        // Compares in constant time, so the token can't be guessed from how fast wrong ones are refused.
        bool IsAuthorized(string header)
        {
            if (header == null)
                return false;
            var actual = Utf8.GetBytes(header);
            if (actual.Length != _expectedAuthorization.Length)
                return false;

            var difference = 0;
            for (var i = 0; i < actual.Length; i++)
                difference |= actual[i] ^ _expectedAuthorization[i];
            return difference == 0;
        }

        static int FreeLoopbackPort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try
            {
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }

        static string NewToken()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
