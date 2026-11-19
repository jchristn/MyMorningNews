namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Research runs: list, read, run now, dry run, and cancel (plan section 14.2).
    /// </summary>
    public class RunRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public RunRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/runs", ListAsync, null, openApiMetadata: Paged(Doc("List runs", "Runs")
                .WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("status", typeof(RunStatusEnum))).WithParameter(QueryParam("trigger", typeof(RunTriggerEnum)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/runs/{id}", ReadAsync, null, openApiMetadata: Doc("Read a run", "Runs"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/runs/{id}/cancel", CancelAsync, null, openApiMetadata: Doc("Cancel a queued or running run", "Runs"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/topics/{id}/run", RunNowAsync, null, openApiMetadata: Doc("Run a topic now", "Runs",
                "Queues a run (202). Add dryRun=true for a dry run that sends no email and is not counted in history.").WithParameter(QueryParam("dryRun", typeof(bool))));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RunRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            RunFilter filter = new RunFilter
            {
                TenantId = tenantId,
                TopicId = Query(ctx, "topicId"),
                Status = QueryEnum<RunStatusEnum>(ctx, "status"),
                Trigger = QueryEnum<RunTriggerEnum>(ctx, "trigger"),
                UserId = ListScope(ctx)
            };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.Runs.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            Run? run = await OwnedRunAsync(ctx, RequestTypeEnum.RunRead).ConfigureAwait(false);
            if (run == null) return;
            await SendJsonAsync(ctx, 200, run).ConfigureAwait(false);
        }

        private async Task CancelAsync(HttpContextBase ctx)
        {
            Run? run = await OwnedRunAsync(ctx, RequestTypeEnum.RunExecute).ConfigureAwait(false);
            if (run == null) return;
            await SendJsonAsync(ctx, 200, await Server.Scheduling.CancelAsync(run, DateTime.UtcNow, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task RunNowAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RunExecute).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Topic? topic = await Server.Database.Topics.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (topic == null || !IsOwnerOrAdmin(ctx, topic.UserId))
            {
                await SendNotFoundAsync(ctx, "Topic").ConfigureAwait(false);
                return;
            }

            Run run = await Server.Scheduling.RunNowAsync(topic, Context(ctx).UserId ?? topic.UserId, QueryBool(ctx, "dryRun") == true, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 202, run).ConfigureAwait(false);
        }

        private async Task<Run?> OwnedRunAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Run? run = await Server.Database.Runs.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (run == null || !IsOwnerOrAdmin(ctx, run.UserId))
            {
                await SendNotFoundAsync(ctx, "Run").ConfigureAwait(false);
                return null;
            }

            return run;
        }
    }
}
