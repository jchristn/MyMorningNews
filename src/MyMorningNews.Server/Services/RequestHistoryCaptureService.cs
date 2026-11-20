namespace MyMorningNews.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Telemetry;
    using SyslogLogging;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Routing;

    /// <summary>
    /// Captures each completed request into request history through a bounded channel. Builds the entry synchronously,
    /// writes it on a background task, and never fails the response. Secret headers are redacted and bodies truncated.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class RequestHistoryCaptureService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Path prefixes never captured (health probes, metrics, the history viewer polling itself).
        /// </summary>
        public List<string> ExcludedPathPrefixes { get; set; } = new List<string> { "/v1.0/api/health", "/metrics", "/favicon.ico" };

        /// <summary>
        /// Path prefixes whose response bodies are never stored (for example credential reveal).
        /// </summary>
        public List<string> RedactedResponsePathPrefixes { get; set; } = new List<string> { "/v1.0/api/vault/", "/v1.0/token" };

        /// <summary>
        /// Path prefixes whose request bodies are never stored.
        /// </summary>
        public List<string> RedactedRequestPathPrefixes { get; set; } = new List<string> { "/v1.0/api/me/password", "/v1.0/api/vault/" };

        #endregion

        #region Private-Members

        private static readonly string[] _SecretHeaders = new[] { "authorization", "proxy-authorization", "cookie", "set-cookie", "x-password", "x-secret-key", "x-signature" };
        private readonly DatabaseDriverBase _Database;
        private readonly RequestHistorySettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly Channel<RequestHistoryEntry> _Channel;
        private readonly CancellationTokenSource _Stop = new CancellationTokenSource();
        private readonly Task _Writer;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate and start the background writer.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="settings">Request history settings (read live).</param>
        /// <param name="logging">Logging.</param>
        /// <param name="capacity">Channel capacity. Default 2000. When full, the oldest pending entry is dropped.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public RequestHistoryCaptureService(DatabaseDriverBase database, RequestHistorySettings settings, LoggingModule logging, int capacity = 2000)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logging);
            _Database = database;
            _Settings = settings;
            _Logging = logging;
            _Channel = Channel.CreateBounded<RequestHistoryEntry>(new BoundedChannelOptions(Math.Clamp(capacity, 10, 100000))
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });
            _Writer = Task.Run(() => WriteLoopAsync(_Stop.Token));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Capture a completed request. Never throws.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        public void Capture(HttpContextBase ctx)
        {
            if (ctx == null || !_Settings.Enabled) return;
            try
            {
                string path = ctx.Request.Url.RawWithoutQuery ?? String.Empty;
                if (ctx.Request.Method == HttpMethod.OPTIONS) return;
                if (ExcludedPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return;

                RequestContext? rc = ctx.Metadata as RequestContext;
                RequestHistoryEntry entry = new RequestHistoryEntry
                {
                    Id = MyMorningNews.Core.Helpers.IdGenerator.GenerateRequestHistoryId(),
                    TenantId = rc?.TenantId,
                    UserId = rc?.UserId,
                    PrincipalName = rc?.DisplayName,
                    Method = ctx.Request.Method.ToString(),
                    Path = RedactPath(path),
                    RouteTemplate = RouteTemplate(ctx),
                    Url = RedactPath(ctx.Request.Url.RawWithQuery ?? path),
                    StatusCode = ctx.Response.StatusCode,
                    DurationMs = Math.Round(ctx.Timestamp.TotalMs ?? 0, 2),
                    SourceIp = ctx.Request.Source?.IpAddress,
                    RequestHeaders = Redact(ctx.Request.Headers),
                    ResponseHeaders = Redact(ctx.Response.Headers),
                    CreatedUtc = rc?.ReceivedUtc ?? DateTime.UtcNow,
                    CompletedUtc = DateTime.UtcNow
                };

                bool redactRequest = RedactedRequestPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                byte[] requestBytes = SafeRequestBytes(ctx);
                entry.RequestBodyBytes = requestBytes.Length;
                if (redactRequest && requestBytes.Length > 0) entry.RequestBody = "[redacted]";
                else Truncate(requestBytes, _Settings.MaxRequestBodyBytes, out string? reqBody, out bool reqTruncated, entry, true);

                bool redactResponse = RedactedResponsePathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                byte[] responseBytes = SafeResponseBytes(ctx);
                entry.ResponseBodyBytes = responseBytes.Length;
                if (redactResponse && responseBytes.Length > 0) entry.ResponseBody = "[redacted]";
                else Truncate(responseBytes, _Settings.MaxResponseBodyBytes, out string? resBody, out bool resTruncated, entry, false);

                if (!_Channel.Writer.TryWrite(entry)) MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.RequestCaptures, "outcome", "dropped");
            }
            catch (Exception ex)
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.RequestCaptures, "outcome", "error");
                _Logging.Debug("[RequestHistoryCapture] capture failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Dispose of resources, flushing pending entries for up to two seconds.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                _Channel.Writer.TryComplete();
                try
                {
                    _Writer.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                }

                _Stop.Cancel();
                _Stop.Dispose();
            }

            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private async Task WriteLoopAsync(CancellationToken token)
        {
            try
            {
                while (await _Channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    while (_Channel.Reader.TryRead(out RequestHistoryEntry? entry))
                    {
                        try
                        {
                            await _Database.RequestHistory.CreateAsync(entry, token).ConfigureAwait(false);
                            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.RequestCaptures, "outcome", "stored");
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.RequestCaptures, "outcome", "error");
                            _Logging.Warn("[RequestHistoryCapture] unable to store request " + entry.Id + ": " + ex.Message);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static string RedactPath(string path)
        {
            const string marker = "/v1.0/public/";
            int i = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return path;
            string[] parts = path.Substring(i + marker.Length).Split('/');
            if (parts.Length >= 2 && parts[1].Length > 6) parts[1] = parts[1].Substring(0, 6) + "...";
            return path.Substring(0, i + marker.Length) + String.Join("/", parts);
        }

        private static string? RouteTemplate(HttpContextBase ctx)
        {
            if (ctx.Route is StaticRoute s) return s.Path;
            if (ctx.Route is ParameterRoute p) return p.Path;
            return null;
        }

        private static Dictionary<string, string> Redact(NameValueCollection? headers)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null) return result;
            foreach (string? key in headers.AllKeys)
            {
                if (key == null) continue;
                string lower = key.ToLowerInvariant();
                bool secret = _SecretHeaders.Contains(lower) || lower.Contains("api-key", StringComparison.Ordinal) || lower.Contains("token", StringComparison.Ordinal);
                result[key] = secret ? "[redacted]" : (headers[key] ?? String.Empty);
            }

            return result;
        }

        private static byte[] SafeRequestBytes(HttpContextBase ctx)
        {
            try
            {
                if (ctx.Request.ContentLength <= 0 && !ctx.Request.ChunkedTransfer) return Array.Empty<byte>();
                return ctx.Request.DataAsBytes ?? Array.Empty<byte>();
            }
            catch (Exception)
            {
                return Array.Empty<byte>();
            }
        }

        private static byte[] SafeResponseBytes(HttpContextBase ctx)
        {
            try
            {
                if (ctx.Response.ServerSentEvents || ctx.Response.ChunkedTransfer) return Array.Empty<byte>();
                return ctx.Response.DataAsBytes ?? Array.Empty<byte>();
            }
            catch (Exception)
            {
                return Array.Empty<byte>();
            }
        }

        private static void Truncate(byte[] data, int max, out string? body, out bool truncated, RequestHistoryEntry entry, bool request)
        {
            body = null;
            truncated = false;
            if (data.Length == 0) return;
            int take = Math.Min(data.Length, max);
            truncated = data.Length > max;
            if (LooksBinary(data, take))
            {
                body = "[binary, " + data.Length + " bytes]";
                truncated = false;
            }
            else
            {
                body = Encoding.UTF8.GetString(data, 0, take);
            }

            if (request)
            {
                entry.RequestBody = body;
                entry.RequestBodyTruncated = truncated;
            }
            else
            {
                entry.ResponseBody = body;
                entry.ResponseBodyTruncated = truncated;
            }
        }

        private static bool LooksBinary(byte[] data, int length)
        {
            int scan = Math.Min(length, 8192);
            int control = 0;
            for (int i = 0; i < scan; i++)
            {
                byte b = data[i];
                if (b == 0) return true;
                if (b < 0x09 || (b > 0x0D && b < 0x20)) control++;
            }

            return scan > 0 && control * 10 > scan;
        }

        #endregion
    }
}
