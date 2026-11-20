namespace MyMorningNews.McpServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// MCP server entry point. Streamable HTTP by default (<c>/mcp</c> on port 12105, health on <c>/</c>), or stdio with
    /// <c>--stdio</c>. Diagnostics go to standard error so stdio's standard output carries only MCP messages.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Arguments: <c>--stdio</c>, <c>--api-url</c>, <c>--host</c>, <c>--port</c>, <c>--verbose</c>.</param>
        /// <returns>Exit code: 0 on a clean stop, 2 for bad arguments, 1 for other failures.</returns>
        public static async Task<int> Main(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            McpServerSettings settings;
            try
            {
                settings = McpServerSettings.FromArguments(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }

            Action<string> log = message => Console.Error.WriteLine(message);
            using (CancellationTokenSource stop = new CancellationTokenSource())
            using (McpTelemetryHost telemetry = new McpTelemetryHost())
            using (McpHost host = new McpHost(settings, log))
            {
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    stop.Cancel();
                };
                AppDomain.CurrentDomain.ProcessExit += (s, e) => stop.Cancel();
                telemetry.Start(settings, log);
                try
                {
                    if (settings.Stdio) await host.RunStdioAsync(stop.Token).ConfigureAwait(false);
                    else await host.RunHttpAsync(stop.Token).ConfigureAwait(false);
                    return 0;
                }
                catch (OperationCanceledException)
                {
                    return 0;
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is TimeoutException)
                {
                    log(ex.Message);
                    return 1;
                }
            }
        }
    }
}
