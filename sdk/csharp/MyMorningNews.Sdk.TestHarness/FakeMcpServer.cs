namespace MyMorningNews.Sdk.TestHarness
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A minimal Streamable HTTP MCP server on 127.0.0.1 for the MCP registry cases. It answers initialize, ping, and
    /// tools/list with JSON (no event stream), accepts notifications with 202, and lists two harmless tools. It never runs a
    /// tool. Thread safety: requests are handled concurrently; the tool list is fixed.
    /// </summary>
    public class FakeMcpServer : IDisposable
    {
        /// <summary>
        /// The MCP endpoint, for example http://127.0.0.1:54321/mcp.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Names of the tools the server lists. Never null.
        /// </summary>
        public IReadOnlyList<string> ToolNames { get; } = new List<string> { "harness_echo", "harness_time" };

        /// <summary>
        /// Number of HTTP requests received so far.
        /// </summary>
        public int RequestCount
        {
            get
            {
                return Interlocked.CompareExchange(ref _RequestCount, 0, 0);
            }
        }

        private readonly HttpListener _Listener = new HttpListener();
        private readonly CancellationTokenSource _Stop = new CancellationTokenSource();
        private readonly Task _Loop;
        private int _RequestCount = 0;
        private bool _Disposed = false;

        /// <summary>
        /// Start listening on a free loopback port.
        /// </summary>
        /// <exception cref="HttpListenerException">The listener could not start.</exception>
        public FakeMcpServer()
        {
            int port = FreePort();
            _Listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            _Listener.Start();
            Url = "http://127.0.0.1:" + port + "/mcp";
            _Loop = Task.Run(() => LoopAsync(_Stop.Token));
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                _Stop.Cancel();
                try
                {
                    _Listener.Stop();
                    _Listener.Close();
                }
                catch (ObjectDisposedException)
                {
                }

                try
                {
                    _Loop.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException)
                {
                }

                _Stop.Dispose();
            }

            _Disposed = true;
        }

        private static int FreePort()
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
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

        private async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(ctx), CancellationToken.None);
            }
        }

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            Interlocked.Increment(ref _RequestCount);
            try
            {
                if (ctx.Request.HttpMethod == "DELETE")
                {
                    ctx.Response.StatusCode = 200;
                    return;
                }

                if (ctx.Request.HttpMethod != "POST")
                {
                    ctx.Response.StatusCode = 405;
                    return;
                }

                string body;
                using (StreamReader reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                JsonNode? request = JsonNode.Parse(body);
                JsonNode? id = request?["id"];
                if (request == null || id == null)
                {
                    ctx.Response.StatusCode = 202;
                    return;
                }

                string method = request["method"]?.GetValue<string>() ?? String.Empty;
                JsonObject reply = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone() };
                if (method == "initialize")
                {
                    string version = request["params"]?["protocolVersion"]?.GetValue<string>() ?? "2025-11-25";
                    reply["result"] = new JsonObject
                    {
                        ["protocolVersion"] = version,
                        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                        ["serverInfo"] = new JsonObject { ["name"] = "sdk-harness-fake", ["version"] = "1.0.0" }
                    };
                    ctx.Response.AddHeader("Mcp-Session-Id", Guid.NewGuid().ToString("N"));
                }
                else if (method == "tools/list")
                {
                    JsonArray tools = new JsonArray();
                    foreach (string name in ToolNames)
                    {
                        tools.Add(new JsonObject
                        {
                            ["name"] = name,
                            ["description"] = "Harness tool " + name + "; never called.",
                            ["inputSchema"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() }
                        });
                    }

                    reply["result"] = new JsonObject { ["tools"] = tools };
                }
                else if (method == "ping")
                {
                    reply["result"] = new JsonObject();
                }
                else
                {
                    reply["error"] = new JsonObject { ["code"] = -32601, ["message"] = "Method not found: " + method };
                }

                byte[] bytes = Encoding.UTF8.GetBytes(reply.ToJsonString());
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is HttpListenerException || ex is InvalidOperationException)
            {
                try
                {
                    ctx.Response.StatusCode = 400;
                }
                catch (InvalidOperationException)
                {
                }
            }
            finally
            {
                try
                {
                    ctx.Response.Close();
                }
                catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException)
                {
                }
            }
        }
    }
}
