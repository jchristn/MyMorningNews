namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Request history routes at /v1.0/api/request-history. Tenant scoped; global administrators may pass ?tenantId= or see all.
    /// When capture is disabled the routes still answer with empty results.
    /// </summary>
    public class RequestHistoryRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public RequestHistoryRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/request-history", ListAsync, null, openApiMetadata: Paged(Doc("List captured requests", "Request History").WithParameter(QueryParam("tenantId", typeof(string))).WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("method", typeof(string))).WithParameter(QueryParam("statusCode", typeof(int))).WithParameter(QueryParam("minStatusCode", typeof(int))).WithParameter(QueryParam("maxStatusCode", typeof(int))).WithParameter(QueryParam("statusClass", typeof(string))).WithParameter(QueryParam("pathContains", typeof(string))).WithParameter(QueryParam("bucketMinutes", typeof(int)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/request-history" + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("List captured requests", "Request History").WithParameter(QueryParam("tenantId", typeof(string))).WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("method", typeof(string))).WithParameter(QueryParam("statusCode", typeof(int))).WithParameter(QueryParam("minStatusCode", typeof(int))).WithParameter(QueryParam("maxStatusCode", typeof(int))).WithParameter(QueryParam("statusClass", typeof(string))).WithParameter(QueryParam("pathContains", typeof(string))).WithParameter(QueryParam("bucketMinutes", typeof(int)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/request-history/summary", SummaryAsync, null,
                openApiMetadata: Doc("Time-bucketed request counts", "Request History", "Requires fromUtc, toUtc, and bucketMinutes.").WithParameter(QueryParam("fromUtc", typeof(DateTime))).WithParameter(QueryParam("toUtc", typeof(DateTime))).WithParameter(QueryParam("bucketMinutes", typeof(int))).WithParameter(QueryParam("tenantId", typeof(string))).WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("method", typeof(string))).WithParameter(QueryParam("pathContains", typeof(string))).WithParameter(QueryParam("search", typeof(string))).WithParameter(QueryParam("statusCode", typeof(int))).WithParameter(QueryParam("statusClass", typeof(int))).WithParameter(QueryParam("minStatusCode", typeof(int))).WithParameter(QueryParam("maxStatusCode", typeof(int))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.DELETE, "/v1.0/api/request-history", DeleteManyAsync, null, openApiMetadata: Doc("Delete requests matching a filter", "Request History").WithParameter(QueryParam("fromUtc", typeof(DateTime))).WithParameter(QueryParam("toUtc", typeof(DateTime))).WithParameter(QueryParam("search", typeof(string))).WithParameter(QueryParam("tenantId", typeof(string))).WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("method", typeof(string))).WithParameter(QueryParam("statusCode", typeof(int))).WithParameter(QueryParam("minStatusCode", typeof(int))).WithParameter(QueryParam("maxStatusCode", typeof(int))).WithParameter(QueryParam("statusClass", typeof(string))).WithParameter(QueryParam("pathContains", typeof(string))).WithParameter(QueryParam("bucketMinutes", typeof(int))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/request-history/{id}", ReadAsync, null, openApiMetadata: Doc("Read a captured request with headers and bodies", "Request History"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/request-history/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a captured request", "Request History"));
        }

        private RequestHistoryFilter BuildFilter(HttpContextBase ctx)
        {
            RequestContext rc = Context(ctx);
            RequestHistoryFilter filter = new RequestHistoryFilter
            {
                TenantId = rc.IsAdmin ? Query(ctx, "tenantId") : rc.TenantId,
                UserId = rc.IsAdmin ? Query(ctx, "userId") : null,
                Method = Query(ctx, "method"),
                StatusCode = QueryInt(ctx, "statusCode"),
                MinStatusCode = QueryInt(ctx, "minStatusCode") ?? ClassMinimum(Query(ctx, "statusClass")),
                MaxStatusCode = QueryInt(ctx, "maxStatusCode") ?? ClassMaximum(Query(ctx, "statusClass")),
                PathContains = Query(ctx, "pathContains")
            };
            ApplyPaging(ctx, filter);
            int? bucket = QueryInt(ctx, "bucketMinutes");
            if (bucket.HasValue) filter.BucketMinutes = bucket.Value;
            if (!rc.IsAdmin && filter.TenantId == null) filter.TenantId = "__none__";
            return filter;
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RequestHistoryRead).ConfigureAwait(false)) return;
            RequestHistoryFilter filter = BuildFilter(ctx);
            if (!Server.Settings.RequestHistory.Enabled)
            {
                await SendJsonAsync(ctx, 200, EnumerationResult<RequestHistoryEntry>.Create(new List<RequestHistoryEntry>(), filter, 0, 0)).ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, await Server.Database.RequestHistory.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task SummaryAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RequestHistoryRead).ConfigureAwait(false)) return;
            RequestHistoryFilter filter = BuildFilter(ctx);
            if (!filter.FromUtc.HasValue || !filter.ToUtc.HasValue || QueryInt(ctx, "bucketMinutes") == null)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "fromUtc, toUtc, and bucketMinutes are required.").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, await Server.Database.RequestHistory.SummarizeAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RequestHistoryRead).ConfigureAwait(false)) return;
            RequestContext rc = Context(ctx);
            RequestHistoryEntry? entry = await Server.Database.RequestHistory.ReadAsync(rc.IsAdmin ? null : rc.TenantId ?? "__none__", Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (entry == null)
            {
                await SendNotFoundAsync(ctx, "Request").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, entry).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RequestHistoryDelete).ConfigureAwait(false)) return;
            RequestContext rc = Context(ctx);
            bool deleted = await Server.Database.RequestHistory.DeleteAsync(rc.IsAdmin ? null : rc.TenantId ?? "__none__", Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (!deleted)
            {
                await SendNotFoundAsync(ctx, "Request").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task DeleteManyAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RequestHistoryDelete).ConfigureAwait(false)) return;
            int deleted = await Server.Database.RequestHistory.DeleteManyAsync(BuildFilter(ctx), ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new DeleteManyResponse { DeletedCount = deleted }).ConfigureAwait(false);
        }

        private static int? ClassMinimum(string? statusClass)
        {
            if (String.IsNullOrWhiteSpace(statusClass) || statusClass.Length != 3 || !Char.IsDigit(statusClass[0])) return null;
            if (!statusClass.EndsWith("xx", StringComparison.OrdinalIgnoreCase)) return null;
            return (statusClass[0] - '0') * 100;
        }

        private static int? ClassMaximum(string? statusClass)
        {
            int? min = ClassMinimum(statusClass);
            return min.HasValue ? min.Value + 99 : null;
        }
    }
}
