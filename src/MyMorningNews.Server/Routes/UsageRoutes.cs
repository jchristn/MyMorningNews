namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Model usage and cost analytics: grouped reports (by user, model, task, provider, topic, prompt, day, or tenant) and time series.
    /// </summary>
    public class UsageRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public UsageRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/usage/report", ReportAsync, null, openApiMetadata: Doc("Model usage and cost report", "Usage",
                "Query: groupBy (User, Model, Task, Provider, Topic, PromptKey, Day, Tenant), from, to, userId, topicId, modelEndpointId, task, includeDryRun, top, allTenants (global administrators).").WithParameter(QueryParam("to", typeof(DateTime))).WithParameter(QueryParam("from", typeof(DateTime))).WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("modelEndpointId", typeof(string))).WithParameter(QueryParam("includeDryRun", typeof(bool))).WithParameter(QueryParam("top", typeof(int))).WithParameter(QueryParam("bucketMinutes", typeof(int))).WithParameter(QueryParam("allTenants", typeof(bool))).WithParameter(QueryParam("groupBy", typeof(UsageGroupByEnum))).WithParameter(QueryParam("task", typeof(ModelTaskEnum))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/usage/timeseries", TimeseriesAsync, null, openApiMetadata: Doc("Model usage and cost time series", "Usage",
                "Query: from, to, bucketMinutes (default 1440), and the same filters as the report.").WithParameter(QueryParam("to", typeof(DateTime))).WithParameter(QueryParam("from", typeof(DateTime))).WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("modelEndpointId", typeof(string))).WithParameter(QueryParam("includeDryRun", typeof(bool))).WithParameter(QueryParam("top", typeof(int))).WithParameter(QueryParam("bucketMinutes", typeof(int))).WithParameter(QueryParam("allTenants", typeof(bool))).WithParameter(QueryParam("groupBy", typeof(UsageGroupByEnum))).WithParameter(QueryParam("task", typeof(ModelTaskEnum))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/usage/me", MineAsync, null, openApiMetadata: Doc("The caller's own model usage and cost", "Usage").WithParameter(QueryParam("to", typeof(DateTime))).WithParameter(QueryParam("from", typeof(DateTime))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("modelEndpointId", typeof(string))).WithParameter(QueryParam("includeDryRun", typeof(bool))).WithParameter(QueryParam("top", typeof(int))).WithParameter(QueryParam("bucketMinutes", typeof(int))).WithParameter(QueryParam("groupBy", typeof(UsageGroupByEnum))).WithParameter(QueryParam("task", typeof(ModelTaskEnum))));
        }

        private async Task ReportAsync(HttpContextBase ctx)
        {
            UsageReportFilter? filter = await BuildFilterAsync(ctx).ConfigureAwait(false);
            if (filter == null) return;
            await SendJsonAsync(ctx, 200, await Server.Database.UsageAnalytics.ReportAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task TimeseriesAsync(HttpContextBase ctx)
        {
            UsageReportFilter? filter = await BuildFilterAsync(ctx).ConfigureAwait(false);
            if (filter == null) return;
            try
            {
                await SendJsonAsync(ctx, 200, await Server.Database.UsageAnalytics.TimeseriesAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", MyMorningNews.Core.Helpers.ErrorText.Plain(ex)).ConfigureAwait(false);
            }
        }

        private async Task MineAsync(HttpContextBase ctx)
        {
            RequestContext rc = Context(ctx);
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (String.IsNullOrEmpty(rc.UserId))
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "Usage for the caller requires a user session or user credential.").ConfigureAwait(false);
                return;
            }

            UsageReportFilter filter = FromQuery(ctx, tenantId);
            filter.UserId = rc.UserId;
            filter.AllTenants = false;
            filter.GroupBy = QueryEnum<UsageGroupByEnum>(ctx, "groupBy") ?? UsageGroupByEnum.Task;
            await SendJsonAsync(ctx, 200, await Server.Database.UsageAnalytics.ReportAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task<UsageReportFilter?> BuildFilterAsync(HttpContextBase ctx)
        {
            RequestContext rc = Context(ctx);
            bool allTenants = QueryBool(ctx, "allTenants") == true;
            if (allTenants)
            {
                if (!rc.IsAdmin)
                {
                    await SendErrorAsync(ctx, 403, "Forbidden", "Platform-wide usage requires a global administrator.").ConfigureAwait(false);
                    return null;
                }

                UsageReportFilter global = FromQuery(ctx, null);
                global.AllTenants = true;
                return global;
            }

            if (!await AuthorizeAsync(ctx, RequestTypeEnum.UsageRead).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            if (QueryEnum<UsageGroupByEnum>(ctx, "groupBy") == UsageGroupByEnum.Tenant)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "Grouping by tenant requires allTenants=true.").ConfigureAwait(false);
                return null;
            }

            return FromQuery(ctx, tenantId);
        }

        private static UsageReportFilter FromQuery(HttpContextBase ctx, string? tenantId)
        {
            DateTime to = QueryDate(ctx, "to") ?? DateTime.UtcNow;
            DateTime from = QueryDate(ctx, "from") ?? to.AddDays(-30);
            return new UsageReportFilter
            {
                TenantId = tenantId,
                FromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc),
                ToUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc),
                GroupBy = QueryEnum<UsageGroupByEnum>(ctx, "groupBy") ?? UsageGroupByEnum.Model,
                UserId = Query(ctx, "userId"),
                TopicId = Query(ctx, "topicId"),
                ModelEndpointId = Query(ctx, "modelEndpointId"),
                Task = QueryEnum<ModelTaskEnum>(ctx, "task"),
                IncludeDryRun = QueryBool(ctx, "includeDryRun"),
                Top = Math.Clamp(QueryInt(ctx, "top") ?? 50, 1, 1000),
                BucketMinutes = Math.Clamp(QueryInt(ctx, "bucketMinutes") ?? 1440, 1, 43200)
            };
        }
    }
}
