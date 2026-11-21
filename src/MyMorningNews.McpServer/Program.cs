namespace MyMorningNews.McpServer
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// MCP server entry point. Streamable HTTP by default (<c>/mcp</c> on port 12105, health on <c>/</c>), or stdio with
    /// <c>--stdio</c>. Diagnostics go to standard error so stdio's standard output carries only MCP messages; protocol
    /// messages are written there only with <c>--verbose</c>.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Arguments: <c>--stdio</c>, <c>--api-url</c>, <c>--host</c>, <c>--port</c>, <c>--verbose</c>, <c>--describe</c>.</param>
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

            if (settings.Describe)
            {
                Console.Out.WriteLine(McpSurface.DescribeJson());
                return 0;
            }

            // The stdio transport library writes every message it sends and receives straight to standard error. Keep the
            // real standard error for our own diagnostics, and let protocol messages through only with --verbose (they
            // then arrive through the server's Log event).
            TextWriter stderr = Console.Error;
            Action<string> log = message => stderr.WriteLine(message);
            if (settings.Stdio) Console.SetError(TextWriter.Null);

            using (CancellationTokenSource stop = new CancellationTokenSource())
            using (McpTelemetryHost telemetry = new McpTelemetryHost())
            using (McpHost host = new McpHost(settings, log))
            {
                int stopped = 0;
                Action requestStop = () =>
                {
                    // A signal can arrive after the run has ended and the token source is gone; ignore it then.
                    if (Volatile.Read(ref stopped) != 0) return;
                    try
                    {
                        stop.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                };
                ConsoleCancelEventHandler onCancel = (s, e) =>
                {
                    e.Cancel = true;
                    requestStop();
                };
                EventHandler onExit = (s, e) => requestStop();
                Console.CancelKeyPress += onCancel;
                AppDomain.CurrentDomain.ProcessExit += onExit;
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
                finally
                {
                    Interlocked.Exchange(ref stopped, 1);
                    Console.CancelKeyPress -= onCancel;
                    AppDomain.CurrentDomain.ProcessExit -= onExit;
                }
            }
        }
    }
}
