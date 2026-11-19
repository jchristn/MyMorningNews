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

        private readonly ServerContext _Server;
        private readonly RequestHistoryCaptureService _Capture;
        private readonly string _Header = "[RequestPipeline] ";

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
        public Task PreRoutingAsync(HttpContextBase ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            RequestContext rc = new RequestContext
            {
                Method = ctx.Request.Method.ToString(),
                Path = ctx.Request.Url.RawWithoutQuery ?? String.Empty,
                SourceIp = ctx.Request.Source?.IpAddress,
                UserAgent = ctx.Request.Useragent,
                CorrelationId = Trim(ctx.Request.RetrieveHeaderValue("x-request"), 128),
                Locale = Trim(ctx.Request.RetrieveHeaderValue("accept-language"), 64),
                TraceId = Activity.Current?.TraceId.ToString(),
                ReceivedUtc = DateTime.UtcNow
            };
            ctx.Metadata = rc;
            AddCorsHeaders(ctx);
            ctx.Response.Headers.Set("X-Request-Id", rc.RequestId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Authenticate post-authentication routes. Sends 401 when authentication fails.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        public async Task AuthenticateAsync(HttpContextBase ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            RequestContext rc = ctx.Metadata as RequestContext ?? new RequestContext();
            ctx.Metadata = rc;

            AuthenticationContext auth = await _Server.Authentication.AuthenticateAsync(new AuthenticationRequest
            {
                GetHeader = name => ctx.Request.RetrieveHeaderValue(name),
                SourceIp = rc.SourceIp,
                UserAgent = rc.UserAgent
            }, ctx.Token).ConfigureAwait(false);

            rc.ApplyAuthentication(auth);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.AuthRequests, "scheme", auth.Scheme?.ToString() ?? "none", "result", auth.Outcome.ToString());

            if (auth.IsAuthenticated) return;

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
                case JsonException:
                    status = 400; error = "BadRequest"; message = "The request body is not valid JSON."; break;
                case ArgumentException ae:
                    status = 400; error = "ValidationFailed"; message = ae.Message; break;
                case KeyNotFoundException knf:
                    status = 404; error = "NotFound"; message = String.IsNullOrWhiteSpace(knf.Message) || knf.Message.StartsWith("The given key", StringComparison.Ordinal) ? "The requested resource was not found." : knf.Message; break;
                case ConflictException ce:
                    status = 409; error = "Conflict"; message = ce.Message; break;
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
                    status = 409; error = "Conflict"; message = "A record with the same unique value already exists."; break;
                case PostgresException pe when pe.SqlState == PostgresErrorCodes.ForeignKeyViolation:
                    status = 409; error = "Conflict"; message = "The operation references a record that does not exist or is still in use."; break;
                case OperationCanceledException:
                    status = 499; error = "Canceled"; message = "The request was canceled."; break;
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

        private void AddCorsHeaders(HttpContextBase ctx)
        {
            // Watson's OpenAPI document handler adds its own Access-Control-Allow-Origin; setting it here too would send two values.
            if (!String.Equals(ctx.Request.Url.RawWithoutQuery, OpenApiDocumentPath, StringComparison.Ordinal))
                ctx.Response.Headers.Set("Access-Control-Allow-Origin", _Server.Settings.Rest.CorsAllowedOrigins);
            ctx.Response.Headers.Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, HEAD");
            ctx.Response.Headers.Set("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Key, X-Token, X-Email, X-Password, X-Tenant-Guid, X-Access-Key, X-Secret-Key, X-Request, Accept-Language");
            ctx.Response.Headers.Set("Access-Control-Expose-Headers", "X-Request-Id");
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
