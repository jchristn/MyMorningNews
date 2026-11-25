namespace MyMorningNews.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Telemetry;
    using Npgsql;
    using WatsonWebserver.Core;

    /// <summary>
    /// Watson routing hooks: pre-routing (request context, CORS, request ID), authentication, post-routing (capture and
    /// logging), and the exception handler that maps errors to typed responses without stack traces.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class RequestPipeline
    {
        #region Private-Members

        private const string OpenApiDocumentPath = "/openapi.json";
        private const string PublicPrefix = "/v1.0/public/";
        private const string WellKnownPrefix = "/.well-known/";

        private readonly ServerContext _Server;
        private readonly RequestHistoryCaptureService _Capture;
        private readonly string _Header = "[RequestPipeline] ";
        private readonly MyMorningNews.Core.Services.LoginRateLimiter _PerIp = new MyMorningNews.Core.Services.LoginRateLimiter(600);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <param name="capture">Request capture service.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public RequestPipeline(ServerContext server, RequestHistoryCaptureService capture)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(capture);
            _Server = server;
            _Capture = capture;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Answer CORS preflight requests.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        public async Task PreflightAsync(HttpContextBase ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ctx.Response.StatusCode = 200;
            AddCorsHeaders(ctx);
            ctx.Response.Headers.Set("Access-Control-Max-Age", "86400");
            await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create the request context, attach it to the HTTP context, and set CORS and request ID headers.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        public async Task PreRoutingAsync(HttpContextBase ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            RequestContext rc = new RequestContext
            {
                Method = ctx.Request.Method.ToString(),
                Path = ctx.Request.Url.RawWithoutQuery ?? String.Empty,
                SourceIp = ClientAddress(ctx),
                UserAgent = ctx.Request.Useragent,
                CorrelationId = Trim(ctx.Request.RetrieveHeaderValue("x-request"), 128),
                Locale = Trim(ctx.Request.RetrieveHeaderValue("accept-language"), 64),
                TraceId = Activity.Current?.TraceId.ToString(),
                ReceivedUtc = DateTime.UtcNow
            };
            ctx.Metadata = rc;
            AddCorsHeaders(ctx);
            ctx.Response.Headers.Set("X-Request-Id", rc.RequestId);
            int perIp = _Server.Settings.Hosting.RequestsPerMinutePerIp;
            if (perIp > 0 && !String.Equals(rc.Path, MyMorningNews.Core.Constants.HealthPath, StringComparison.Ordinal))
            {
                _PerIp.LimitPerMinute = perIp;
                if (!_PerIp.TryAcquire(rc.SourceIp ?? "unknown"))
                {
                    MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.AuthRequests, "scheme", "ip", "result", "RateLimited");
                    ctx.Response.StatusCode = 429;
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.Headers.Set("Retry-After", "60");
                    await ctx.Response.Send(Serializer.Serialize(new ErrorResponse { Error = "TooManyRequests", Message = "Too many requests from this address. Wait a minute and try again.", RequestId = rc.RequestId }), ctx.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Authenticate post-authentication routes. Sends 401 when authentication fails, and 404 for a public or
        /// well-known path that matched no route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        public async Task AuthenticateAsync(HttpContextBase ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            RequestContext rc = ctx.Metadata as RequestContext ?? new RequestContext();
            ctx.Metadata = rc;

            // Every public and well-known route is registered before authentication, so one that reaches this point
            // matched nothing: say so (404) instead of asking for credentials it would never accept.
            string path = ctx.Request.Url.RawWithoutQuery ?? String.Empty;
            if (path.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase) || path.StartsWith(WellKnownPrefix, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Serializer.Serialize(new ErrorResponse
                {
                    Error = "RouteNotFound",
                    Message = "This server has no " + ctx.Request.Method + " " + path + " endpoint.",
                    RequestId = rc.RequestId
                }), ctx.Token).ConfigureAwait(false);
                return;
            }

            AuthenticationContext auth = await _Server.Authentication.AuthenticateAsync(new AuthenticationRequest
            {
                GetHeader = name => ctx.Request.RetrieveHeaderValue(name),
                SourceIp = rc.SourceIp,
                UserAgent = rc.UserAgent,
                Method = ctx.Request.Method.ToString().ToUpperInvariant(),
                PathAndQuery = ctx.Request.Url.RawWithQuery,
                GetBody = () => ctx.Request.DataAsBytes ?? Array.Empty<byte>()
            }, ctx.Token).ConfigureAwait(false);

            rc.ApplyAuthentication(auth);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.AuthRequests, "scheme", auth.Scheme?.ToString() ?? "none", "result", auth.Outcome.ToString());

            if (auth.IsAuthenticated)
            {
                if (_Server.Activity != null) await _Server.Activity.TouchAsync(rc.TenantId, rc.UserId, ctx.Token).ConfigureAwait(false);
                return;
            }

            if (auth.Outcome != AuthenticationOutcomeEnum.NotAttempted)
                await _Server.Audit.RecordAsync(AuditEventTypeEnum.AuthFailure, rc, 401, auth.Message, ctx.Token).ConfigureAwait(false);

            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(Serializer.Serialize(new ErrorResponse
            {
                Error = "Unauthorized",
                Message = auth.Outcome == AuthenticationOutcomeEnum.NotAttempted ? "Authentication is required." : (auth.Message ?? "Authentication failed."),
                RequestId = rc.RequestId
            }), ctx.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Post-routing: timing, request capture, and logging.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        public Task PostRoutingAsync(HttpContextBase ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ctx.Timestamp.End = DateTime.UtcNow;
            AddCorsHeaders(ctx);
            _Capture.Capture(ctx);
            _Server.Logging.Debug(
                _Header +
                ctx.Request.Method + " " +
                ctx.Request.Url.RawWithQuery + " " +
                ctx.Response.StatusCode + " " +
                "(" + (ctx.Timestamp.TotalMs.HasValue ? ctx.Timestamp.TotalMs.Value.ToString("F2") : "?") + "ms)");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Map unhandled exceptions to typed responses.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="ex">Exception.</param>
        /// <returns>Task.</returns>
        public async Task ExceptionAsync(HttpContextBase ctx, Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(ex);
            RequestContext? rc = ctx.Metadata as RequestContext;
            int status;
            string error;
            string message;
            Dictionary<string, string>? details = null;
            switch (ex)
            {
                case TemplateInvalidException tie:
                    status = 400; error = "TemplateInvalid"; message = tie.Message;
                    details = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (TemplateProblem p in tie.Problems.Where(p => !p.IsWarning).Take(50)) details["line " + p.Line + (details.ContainsKey("line " + p.Line) ? " (" + details.Count + ")" : String.Empty)] = p.Message;
                    break;
                case MyMorningNews.Core.Entitlements.PromoCodeException pce:
                    status = 409; error = "PromoCodeInvalid"; message = pce.Message;
                    details = new Dictionary<string, string>(StringComparer.Ordinal) { { "reason", pce.Reason } };
                    break;
                case AccountLinkException ale:
                    status = 400; error = ale.Code; message = ale.Message; break;
                case JsonException jex:
                    // RouteBase rethrows a described JsonException (field and expected value) with the original inside.
                    status = 400; error = "BadRequest"; message = jex.InnerException is JsonException ? jex.Message : "The request body is not valid JSON."; break;
                case FieldValidationException fve:
                    status = 400; error = "ValidationFailed"; message = fve.Message;
                    details = new Dictionary<string, string>(StringComparer.Ordinal) { { "field", fve.Field } };
                    break;
                case ArgumentException ae:
                    status = 400; error = "ValidationFailed"; message = ErrorText.Plain(ae); break;
                case KeyNotFoundException knf:
                    status = 404; error = "NotFound"; message = String.IsNullOrWhiteSpace(knf.Message) || knf.Message.StartsWith("The given key", StringComparison.Ordinal) ? "The requested resource was not found." : knf.Message; break;
                case SetupRequiredException sre:
                    status = 409; error = "SetupRequired"; message = sre.Message;
                    details = new Dictionary<string, string>(StringComparer.Ordinal) { { "check", sre.CheckKey } };
                    break;
                case ConflictException ce:
                    status = 409; error = "Conflict"; message = ce.Message; break;
                case EgressBlockedException ebe:
                    status = 400; error = "EgressBlocked"; message = ebe.Message; break;
                case CapReachedException cre:
                    status = 402; error = "CapReached"; message = cre.Message;
                    details = new Dictionary<string, string>(StringComparer.Ordinal) { { "key", cre.Key }, { "limit", cre.Limit } };
                    if (cre.ResetUtc.HasValue) details["resetUtc"] = cre.ResetUtc.Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case ArtifactExpiredException aee:
                    status = 410; error = "ArtifactExpired"; message = aee.Message; break;
                case QuotaExceededException qee:
                    status = 429; error = "QuotaExceeded"; message = qee.Message; break;
                case ModelRefusalException mre:
                    status = 422; error = "ModelRefused"; message = mre.Message; break;
                case StructuredOutputException soe:
                    status = 502; error = "ModelOutputInvalid"; message = soe.Message; break;
                case ModelCallException mce:
                    status = 502; error = "ModelError"; message = mce.Message; break;
                case PostgresException pe when pe.SqlState == PostgresErrorCodes.UniqueViolation:
                    status = 409; error = "Conflict"; message = DatabaseErrorDescriber.UniqueMessage(pe);
                    string? field = DatabaseErrorDescriber.UniqueField(pe);
                    if (field != null) details = new Dictionary<string, string>(StringComparer.Ordinal) { { field, "Already in use." } };
                    break;
                case PostgresException pe when pe.SqlState == PostgresErrorCodes.StringDataRightTruncation:
                    // Request bodies are checked against column sizes first (TextLimitAttribute); this is the backstop.
                    status = 400; error = "ValidationFailed"; message = "A value is longer than its field allows. Shorten it and try again."; break;
                case PostgresException pe when pe.SqlState == PostgresErrorCodes.ForeignKeyViolation:
                    status = 409; error = "Conflict"; message = "The operation references a record that does not exist or is still in use."; break;
                case OperationCanceledException:
                    status = 499; error = "Canceled"; message = "The request was canceled."; break;
                case Exception dbe when DatabaseErrorDescriber.IsUnavailable(dbe) || (dbe.InnerException != null && DatabaseErrorDescriber.IsUnavailable(dbe.InnerException)):
                    status = 503; error = "DatabaseUnavailable"; message = "The database is not reachable right now. Try again in a moment.";
                    ctx.Response.Headers.Set("Retry-After", "10");
                    break;
                default:
                    status = 500; error = "InternalError"; message = "An unexpected error occurred. Reference the request ID when reporting it."; break;
            }

            if (status >= 500) _Server.Logging.Exception(ex, "RequestPipeline", ctx.Request.Method + " " + ctx.Request.Url.RawWithoutQuery + " request " + rc?.RequestId);
            if (ctx.Response.ResponseSent) return;
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(Serializer.Serialize(new ErrorResponse { Error = error, Message = message, RequestId = rc?.RequestId, Details = details }), ctx.Token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private string? ClientAddress(HttpContextBase ctx)
        {
            string? peer = ctx.Request.Source?.IpAddress;
            if (!_Server.Settings.Hosting.TrustForwardedFor || String.IsNullOrEmpty(peer)) return peer;
            if (!System.Net.IPAddress.TryParse(peer, out System.Net.IPAddress? peerAddress) || !MyMorningNews.Core.Security.NetworkGuard.IsBlockedAddress(peerAddress)) return peer;
            string? forwarded = ctx.Request.RetrieveHeaderValue("x-forwarded-for");
            if (String.IsNullOrWhiteSpace(forwarded)) return peer;
            string last = forwarded.Split(',').Select(x => x.Trim()).LastOrDefault(x => x.Length > 0) ?? String.Empty;
            return System.Net.IPAddress.TryParse(last, out System.Net.IPAddress? client) ? client.ToString() : peer;
        }

        private void AddCorsHeaders(HttpContextBase ctx)
        {
            // Watson's OpenAPI document handler adds its own Access-Control-Allow-Origin; setting it here too would send two values.
            if (!String.Equals(ctx.Request.Url.RawWithoutQuery, OpenApiDocumentPath, StringComparison.Ordinal))
                ctx.Response.Headers.Set("Access-Control-Allow-Origin", _Server.Settings.Rest.CorsAllowedOrigins);
            ctx.Response.Headers.Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, HEAD");
            ctx.Response.Headers.Set("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Key, X-Token, X-Email, X-Password, X-Tenant-Guid, X-Access-Key, X-Secret-Key, X-Date, X-Nonce, X-Signature, X-Request, Accept-Language, Range");
            ctx.Response.Headers.Set("Access-Control-Expose-Headers", "X-Request-Id, Content-Disposition, Content-Range, Accept-Ranges, Content-Length, ETag");
        }

        private static string? Trim(string? value, int max)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            string v = value.Trim();
            return v.Length > max ? v.Substring(0, max) : v;
        }

        #endregion
    }
}
