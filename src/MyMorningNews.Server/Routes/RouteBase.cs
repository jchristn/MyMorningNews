namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Telemetry;
    using MyMorningNews.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Base class for route registrars: typed body binding, JSON responses, authorization with audited denials, tenant
    /// resolution for control-plane routes, and query parsing for list endpoints.
    /// </summary>
    public abstract class RouteBase
    {
        #region Protected-Members

        /// <summary>
        /// Shared server dependencies.
        /// </summary>
        protected ServerContext Server { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        protected RouteBase(ServerContext server)
        {
            ArgumentNullException.ThrowIfNull(server);
            Server = server;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register routes.
        /// </summary>
        /// <param name="webserver">Watson server.</param>
        public abstract void Register(Webserver webserver);

        #endregion

        #region Protected-Methods

        /// <summary>
        /// The request context attached in pre-routing.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Request context.</returns>
        /// <exception cref="InvalidOperationException">No context is attached.</exception>
        protected static RequestContext Context(HttpContextBase ctx)
        {
            if (ctx.Metadata is RequestContext rc) return rc;
            throw new InvalidOperationException("No request context is attached to the HTTP context.");
        }

        /// <summary>
        /// OpenAPI metadata for a route.
        /// </summary>
        /// <param name="summary">Summary.</param>
        /// <param name="tag">Tag.</param>
        /// <param name="description">Optional description.</param>
        /// <returns>Metadata.</returns>
        protected static OpenApiRouteMetadata Doc(string summary, string tag, string? description = null)
        {
            OpenApiRouteMetadata metadata = OpenApiRouteMetadata.Create(summary, tag);
            if (!String.IsNullOrEmpty(description)) metadata.Description = description;
            return metadata;
        }

        /// <summary>
        /// OpenAPI JSON request body for a type.
        /// </summary>
        /// <typeparam name="T">Body type.</typeparam>
        /// <returns>Request body metadata.</returns>
        protected static OpenApiRequestBodyMetadata Body<T>()
        {
            return OpenApiRequestBodyMetadata.Json(OpenApiSchemaFactory.For(typeof(T)), null, true);
        }

        /// <summary>
        /// OpenAPI query parameter.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="type">Value type (string, int, bool, DateTime, or an enum).</param>
        /// <returns>Parameter metadata.</returns>
        protected static OpenApiParameterMetadata QueryParam(string name, Type type)
        {
            return OpenApiParameterMetadata.Query(name, null, false, OpenApiSchemaFactory.For(type));
        }

        /// <summary>
        /// Add the standard paging, sorting, search, and date range query parameters.
        /// </summary>
        /// <param name="metadata">Route metadata.</param>
        /// <returns>The same metadata.</returns>
        protected static OpenApiRouteMetadata Paged(OpenApiRouteMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            metadata.WithParameter(QueryParam("pageNumber", typeof(int))).WithParameter(QueryParam("pageSize", typeof(int)))
                .WithParameter(QueryParam("sortBy", typeof(string))).WithParameter(QueryParam("sortDirection", typeof(SortDirectionEnum)))
                .WithParameter(QueryParam("search", typeof(string))).WithParameter(QueryParam("fromUtc", typeof(DateTime))).WithParameter(QueryParam("toUtc", typeof(DateTime)));
            return metadata;
        }

        /// <summary>
        /// Send a JSON response.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="statusCode">Status code.</param>
        /// <param name="body">Body, or null for no content.</param>
        /// <returns>Task.</returns>
        protected static async Task SendJsonAsync(HttpContextBase ctx, int statusCode, object? body)
        {
            ctx.Response.StatusCode = statusCode;
            if (body == null)
            {
                await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
                return;
            }

            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(Serializer.Serialize(body), ctx.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Stream a stored file, honoring a single HTTP Range (bytes=a-b, bytes=a-, bytes=-n) with 206 and Content-Range.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="artifacts">Artifact service.</param>
        /// <param name="artifact">Artifact.</param>
        /// <param name="attachment">Send Content-Disposition attachment.</param>
        /// <returns>Task.</returns>
        /// <exception cref="MyMorningNews.Core.Exceptions.ArtifactExpiredException">The content is gone (410).</exception>
        protected static async Task SendArtifactAsync(HttpContextBase ctx, MyMorningNews.Core.Storage.ArtifactService artifacts, MyMorningNews.Core.Models.Artifact artifact, bool attachment)
        {
            long total = artifact.Bytes;
            long? offset = null;
            long? length = null;
            string? range = ctx.Request.RetrieveHeaderValue("Range");
            if (!String.IsNullOrWhiteSpace(range))
            {
                if (!TryParseRange(range, total, out long start, out long end))
                {
                    ctx.Response.Headers.Add("Content-Range", "bytes */" + total);
                    await SendErrorAsync(ctx, 416, "RangeNotSatisfiable", "The requested range is outside the file.").ConfigureAwait(false);
                    return;
                }

                offset = start;
                length = end - start + 1;
            }

            using (MyMorningNews.Core.Storage.ArtifactReadResult r = await artifacts.OpenAsync(artifact, offset, length, ctx.Token).ConfigureAwait(false))
            {
                ctx.Response.ContentType = artifact.ContentType;
                ctx.Response.Headers.Add("Accept-Ranges", "bytes");
                ctx.Response.Headers.Add("Cache-Control", "private, max-age=300");
                ctx.Response.Headers.Add("ETag", "\"" + artifact.Sha256 + "\"");
                ctx.Response.Headers.Add("Content-Disposition", (attachment ? "attachment" : "inline") + "; filename=\"" + artifact.FileName.Replace("\"", String.Empty) + "\"");
                if (offset.HasValue)
                {
                    ctx.Response.StatusCode = 206;
                    ctx.Response.Headers.Add("Content-Range", "bytes " + r.Offset + "-" + (r.Offset + r.Length - 1) + "/" + r.TotalBytes);
                }
                else
                {
                    ctx.Response.StatusCode = 200;
                }

                await ctx.Response.Send(r.Length, r.Stream, ctx.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Parse a single byte range against a total size.
        /// </summary>
        /// <param name="header">Range header value.</param>
        /// <param name="total">Total bytes.</param>
        /// <param name="start">First byte.</param>
        /// <param name="end">Last byte (inclusive).</param>
        /// <returns>True when satisfiable.</returns>
        protected static bool TryParseRange(string header, long total, out long start, out long end)
        {
            start = 0;
            end = total - 1;
            string h = header.Trim();
            if (!h.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) || total <= 0) return false;
            string spec = h.Substring(6).Split(',')[0].Trim();
            int dash = spec.IndexOf('-');
            if (dash < 0) return false;
            string a = spec.Substring(0, dash).Trim();
            string b = spec.Substring(dash + 1).Trim();
            if (a.Length == 0)
            {
                if (!long.TryParse(b, out long suffix) || suffix <= 0) return false;
                start = Math.Max(0, total - suffix);
                return true;
            }

            if (!long.TryParse(a, out start) || start >= total || start < 0) return false;
            if (b.Length > 0)
            {
                if (!long.TryParse(b, out end) || end < start) return false;
                end = Math.Min(end, total - 1);
            }

            return true;
        }

        /// <summary>
        /// Send an error response.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="statusCode">Status code.</param>
        /// <param name="error">Error code.</param>
        /// <param name="message">Message.</param>
        /// <param name="details">Optional details.</param>
        /// <returns>Task.</returns>
        protected static Task SendErrorAsync(HttpContextBase ctx, int statusCode, string error, string message, Dictionary<string, string>? details = null)
        {
            RequestContext? rc = ctx.Metadata as RequestContext;
            return SendJsonAsync(ctx, statusCode, new ErrorResponse { Error = error, Message = message, RequestId = rc?.RequestId, Details = details });
        }

        /// <summary>
        /// Send 404.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="what">What was not found.</param>
        /// <returns>Task.</returns>
        protected static Task SendNotFoundAsync(HttpContextBase ctx, string what)
        {
            return SendErrorAsync(ctx, 404, "NotFound", what + " was not found.");
        }

        /// <summary>
        /// Read an optional JSON body: an empty body returns null without an error; invalid JSON throws (400).
        /// </summary>
        /// <typeparam name="T">Body type.</typeparam>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Body, or null when empty.</returns>
        /// <exception cref="JsonException">The body is not valid JSON, or a value has the wrong type (400 BadRequest).</exception>
        /// <exception cref="ArgumentException">A value is longer than its field allows or is blank where it cannot be (400 ValidationFailed).</exception>
        protected static Task<T?> ReadOptionalBodyAsync<T>(HttpContextBase ctx) where T : class
        {
            string body = ctx.Request.DataAsString ?? String.Empty;
            if (String.IsNullOrWhiteSpace(body)) return Task.FromResult<T?>(null);
            T? value;
            try
            {
                value = Serializer.Deserialize<T>(body);
            }
            catch (JsonException ex)
            {
                throw new JsonException(JsonErrorDescriber.Describe(ex, typeof(T)), ex);
            }

            Dictionary<string, string> errors = RequestValidator.Validate(value);
            if (errors.Count > 0) throw new ArgumentException(RequestValidator.Summarize(errors));
            return Task.FromResult(value);
        }

        /// <summary>
        /// Deserialize the request body into a typed DTO. Sends 400 and returns null when the body is missing or invalid.
        /// </summary>
        /// <typeparam name="T">DTO type.</typeparam>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>DTO, or null when a 400 was sent.</returns>
        protected static async Task<T?> ReadBodyAsync<T>(HttpContextBase ctx) where T : class
        {
            string body = ctx.Request.DataAsString ?? String.Empty;
            if (String.IsNullOrWhiteSpace(body))
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "A JSON request body is required.").ConfigureAwait(false);
                return null;
            }

            T? value;
            try
            {
                value = Serializer.Deserialize<T>(body);
            }
            catch (JsonException ex)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", JsonErrorDescriber.Describe(ex, typeof(T))).ConfigureAwait(false);
                return null;
            }
            catch (ArgumentException ex)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", ErrorText.Plain(ex)).ConfigureAwait(false);
                return null;
            }

            if (value == null)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "The request body must be a JSON object.").ConfigureAwait(false);
                return null;
            }

            Dictionary<string, string> errors = RequestValidator.Validate(value);
            if (errors.Count > 0)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", RequestValidator.Summarize(errors), errors).ConfigureAwait(false);
                return null;
            }

            return value;
        }

        /// <summary>
        /// Authorize the request. Sends 403 (with an audited denial) and returns false when denied.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="requestType">Request type.</param>
        /// <param name="resourceId">Target resource, when any.</param>
        /// <returns>True when permitted.</returns>
        protected async Task<bool> AuthorizeAsync(HttpContextBase ctx, RequestTypeEnum requestType, string? resourceId = null)
        {
            RequestContext rc = Context(ctx);
            AuthorizationContext result = await Server.Authorization.AuthorizeAsync(rc, requestType, resourceId, ctx.Token).ConfigureAwait(false);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.AuthzRequests, "result", result.Outcome.ToString());

            if (result.IsPermitted)
            {
                if (result.BypassReason != null && result.Operation == OperationTypeEnum.Admin && IsMutation(ctx))
                    await Server.Audit.RecordAsync(AuditEventTypeEnum.AuthorizationBypass, rc, 0, result.BypassReason, ctx.Token).ConfigureAwait(false);
                return true;
            }

            RequiredPermission required = OperationScopeMap.Get(requestType);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.AuthzDenials, "result", result.Outcome.ToString(), "required_permission", required.ToString());
            await Server.Audit.RecordAsync(AuditEventTypeEnum.AuthorizationDenied, rc, 403, result.Reason, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 403, new AuthorizationFailureResponse
            {
                Error = "Forbidden",
                Message = "You do not have permission to perform this operation.",
                RequestId = rc.RequestId,
                Reason = result.Reason ?? "Permission denied.",
                RequiredPermission = required.ToString(),
                RequestType = requestType.ToString(),
                TenantGuid = rc.TenantId,
                ResourceGuid = resourceId
            }).ConfigureAwait(false);
            return false;
        }

        /// <summary>
        /// Resolve the {tenantId} route parameter for control-plane routes. Global administrators may target any tenant;
        /// everyone else may only target their own. Sends 404 and returns null otherwise.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Tenant identifier, or null when a 404 was sent.</returns>
        protected async Task<string?> ResolveTenantAsync(HttpContextBase ctx)
        {
            RequestContext rc = Context(ctx);
            string? tenantId = Param(ctx, "tenantId");
            if (String.IsNullOrEmpty(tenantId))
            {
                await SendNotFoundAsync(ctx, "Tenant").ConfigureAwait(false);
                return null;
            }

            if (!rc.IsAdmin && !String.Equals(rc.TenantId, tenantId, StringComparison.Ordinal))
            {
                await SendNotFoundAsync(ctx, "Tenant").ConfigureAwait(false);
                return null;
            }

            if (!await Server.Database.Tenants.ExistsAsync(tenantId, ctx.Token).ConfigureAwait(false))
            {
                await SendNotFoundAsync(ctx, "Tenant").ConfigureAwait(false);
                return null;
            }

            return tenantId;
        }

        /// <summary>
        /// Refuse with 402 CapReached when the user already has as many topics as the plan allows.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="MyMorningNews.Core.Exceptions.CapReachedException">The topic limit is reached.</exception>
        protected async Task CheckTopicLimitAsync(string tenantId, string userId, System.Threading.CancellationToken token)
        {
            long topics = await Server.Database.Topics.CountAsync(new TopicFilter { TenantId = tenantId, UserId = userId, IsOneTime = false }, token).ConfigureAwait(false);
            await Server.Entitlements.CheckCountAsync(tenantId, userId, "topics.max", topics, "topics", null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Tenant scope for ordinary data-plane routes: the caller's tenant. System administrators must bind a tenant with x-tenant-guid.
        /// Sends 400 and returns null when no tenant can be determined.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Tenant identifier, or null when a 400 was sent.</returns>
        protected static async Task<string?> RequireTenantAsync(HttpContextBase ctx)
        {
            RequestContext rc = Context(ctx);
            if (!String.IsNullOrEmpty(rc.TenantId)) return rc.TenantId;
            await SendErrorAsync(ctx, 400, "TenantRequired", "This operation requires a tenant. Supply x-tenant-guid with the administration key.").ConfigureAwait(false);
            return null;
        }

        /// <summary>
        /// Route parameter value.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="name">Parameter name.</param>
        /// <returns>Value, or null.</returns>
        protected static string? Param(HttpContextBase ctx, string name)
        {
            string? value = ctx.Request.Url.Parameters?[name];
            return String.IsNullOrWhiteSpace(value) ? null : Uri.UnescapeDataString(value);
        }

        /// <summary>
        /// Query string value.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="name">Key.</param>
        /// <returns>Value, or null.</returns>
        protected static string? Query(HttpContextBase ctx, string name)
        {
            string? value = ctx.Request.Query?.Elements?[name];
            if (String.IsNullOrWhiteSpace(value)) return null;
            string decoded = WebUtility.UrlDecode(value).Trim();
            return decoded.Length == 0 ? null : decoded;
        }

        /// <summary>
        /// Integer query value.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="name">Key.</param>
        /// <returns>Value, or null when absent or invalid.</returns>
        protected static int? QueryInt(HttpContextBase ctx, string name)
        {
            string? value = Query(ctx, name);
            return value != null && Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : null;
        }

        /// <summary>
        /// Boolean query value.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="name">Key.</param>
        /// <returns>Value, or null when absent or invalid.</returns>
        protected static bool? QueryBool(HttpContextBase ctx, string name)
        {
            string? value = Query(ctx, name);
            return value != null && Boolean.TryParse(value, out bool b) ? b : null;
        }

        /// <summary>
        /// UTC date query value (ISO 8601).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="name">Key.</param>
        /// <returns>Value, or null when absent or invalid.</returns>
        protected static DateTime? QueryDate(HttpContextBase ctx, string name)
        {
            string? value = Query(ctx, name);
            if (value == null) return null;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime d)
                ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;
        }

        /// <summary>
        /// Enum query value.
        /// </summary>
        /// <typeparam name="T">Enum type.</typeparam>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="name">Key.</param>
        /// <returns>Value, or null when absent or invalid.</returns>
        protected static T? QueryEnum<T>(HttpContextBase ctx, string name) where T : struct, Enum
        {
            string? value = Query(ctx, name);
            return value != null && Enum.TryParse(value, true, out T e) ? e : null;
        }

        /// <summary>
        /// Apply paging, sorting, search, and date range from the query string.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="filter">Filter to populate.</param>
        protected static void ApplyPaging(HttpContextBase ctx, EnumerationFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);
            int? page = QueryInt(ctx, "pageNumber");
            if (page.HasValue) filter.PageNumber = page.Value;
            int? size = QueryInt(ctx, "pageSize");
            if (size.HasValue) filter.PageSize = size.Value;
            filter.SortBy = Query(ctx, "sortBy");
            SortDirectionEnum? dir = QueryEnum<SortDirectionEnum>(ctx, "sortDirection");
            if (dir.HasValue) filter.SortDirection = dir.Value;
            else if (String.Equals(Query(ctx, "sortDirection"), "asc", StringComparison.OrdinalIgnoreCase)) filter.SortDirection = SortDirectionEnum.Ascending;
            filter.Search = Query(ctx, "search");
            filter.FromUtc = QueryDate(ctx, "fromUtc");
            filter.ToUtc = QueryDate(ctx, "toUtc");
        }

        /// <summary>
        /// The owner filter for list routes: members see their own items; administrators see everyone's, or one user's
        /// with userId=..., or their own with mine=true.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>User ID to filter on, or null for every user.</returns>
        protected static string? ListScope(HttpContextBase ctx)
        {
            RequestContext rc = Context(ctx);
            if (!(rc.IsAdmin || rc.IsTenantAdmin)) return rc.UserId;
            return QueryBool(ctx, "mine") == true ? rc.UserId : Query(ctx, "userId");
        }

        /// <summary>
        /// Whether the caller may act on a resource owned by a user: the owner, a tenant administrator, or a global administrator.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="ownerUserId">Owning user.</param>
        /// <returns>True when allowed.</returns>
        protected static bool IsOwnerOrAdmin(HttpContextBase ctx, string? ownerUserId)
        {
            RequestContext rc = Context(ctx);
            return rc.IsAdmin || rc.IsTenantAdmin || (!String.IsNullOrEmpty(ownerUserId) && String.Equals(ownerUserId, rc.UserId, StringComparison.Ordinal));
        }

        /// <summary>
        /// Whether the request mutates state.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>True for POST, PUT, PATCH, and DELETE.</returns>
        protected static bool IsMutation(HttpContextBase ctx)
        {
            HttpMethod m = ctx.Request.Method;
            return m == HttpMethod.POST || m == HttpMethod.PUT || m == HttpMethod.PATCH || m == HttpMethod.DELETE;
        }

        #endregion
    }
}
