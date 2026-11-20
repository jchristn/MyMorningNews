namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Pipeline;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Serialization;
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
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/runs", ListAsync, null, openApiMetadata: Paged(Doc("List runs", "Runs").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("status", typeof(RunStatusEnum))).WithParameter(QueryParam("trigger", typeof(RunTriggerEnum)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/runs/{id}", ReadAsync, null, openApiMetadata: Doc("Read a run", "Runs"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/runs/{id}/cancel", CancelAsync, null, openApiMetadata: Doc("Cancel a queued or running run", "Runs"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/runs/{id}/retry", RetryAsync, null, openApiMetadata: Doc("Retry a finished run", "Runs",
                "fromStage=Synthesize or a later stage (ReviewPredictions, Predict, Suggest, ComposeBriefing, Notify) reruns those stages on the same run with its saved findings, "
                + "and returns it queued (202). No fromStage, or an earlier stage, queues a new run of the topic (202), because search and fetch results are not kept in a resumable form.")
                .WithParameter(QueryParam("fromStage", typeof(string))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/runs/{id}/events", EventsAsync, null, openApiMetadata: Doc("Stream a run's progress (server-sent events)", "Runs",
                "text/event-stream. Events: run (the run, on every change), stage (one stage, when it starts, progresses, or ends), ping (keep-alive every 15 seconds), "
                + "and done (the final run; the stream then closes). A stream opens with the current run (when it is not finished) and every stage, so a reconnect needs no Last-Event-ID; for a finished run, the stages come first and done carries the run. "
                + "Streams close after Rest.EventStreamMaxMinutes; reconnect to continue."));
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

        private async Task RetryAsync(HttpContextBase ctx)
        {
            Run? run = await OwnedRunAsync(ctx, RequestTypeEnum.RunExecute).ConfigureAwait(false);
            if (run == null) return;
            if (!IsFinal(run.Status))
            {
                await SendErrorAsync(ctx, 409, "Conflict", "Only a finished run can be retried; this one is " + run.Status + ".").ConfigureAwait(false);
                return;
            }

            Topic? topic = await Server.Database.Topics.ReadAsync(run.TenantId, run.TopicId, ctx.Token).ConfigureAwait(false);
            if (topic == null)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "The run's topic no longer exists.").ConfigureAwait(false);
                return;
            }

            string? from = Query(ctx, "fromStage");
            int index = from == null ? 0 : ResearchPipeline.DefaultStageNames.ToList().FindIndex(n => String.Equals(n, from, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "fromStage must be one of: " + String.Join(", ", ResearchPipeline.DefaultStageNames) + ".").ConfigureAwait(false);
                return;
            }

            int firstResumable = ResearchPipeline.DefaultStageNames.ToList().IndexOf(ResearchPipeline.FirstResumableStage);
            if (from == null || index < firstResumable)
            {
                Run fresh = await Server.Scheduling.RunNowAsync(topic, Context(ctx).UserId ?? topic.UserId, run.IsDryRun, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
                await SendJsonAsync(ctx, 202, fresh).ConfigureAwait(false);
                return;
            }

            DateTime now = DateTime.UtcNow;
            run.ResumeFromStage = ResearchPipeline.DefaultStageNames[index];
            run.Status = RunStatusEnum.Queued;
            run.QueuedUtc = now;
            run.CompletedUtc = null;
            run.LockedUntilUtc = null;
            run.WorkerId = null;
            run.CancelRequested = false;
            run.ErrorMessage = null;
            run.SkippedReason = null;
            await SendJsonAsync(ctx, 202, await Server.Database.Runs.UpdateAsync(run, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task EventsAsync(HttpContextBase ctx)
        {
            Run? run = await OwnedRunAsync(ctx, RequestTypeEnum.RunRead).ConfigureAwait(false);
            if (run == null) return;
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Set("Cache-Control", "no-cache");
            ctx.Response.Headers.Set("X-Accel-Buffering", "no");
            ctx.Response.ServerSentEvents = true;

            long sequence = 0;
            string lastRun = String.Empty;
            Dictionary<string, string> lastStages = new Dictionary<string, string>(StringComparer.Ordinal);
            DateTime deadline = DateTime.UtcNow.AddMinutes(Server.Settings.Rest.EventStreamMaxMinutes);
            DateTime nextPing = DateTime.UtcNow.AddSeconds(15);
            int pollMs = Server.Settings.Rest.EventStreamPollMs;
            while (!ctx.Token.IsCancellationRequested)
            {
                Run? current = await Server.Database.Runs.ReadAsync(run.TenantId, run.Id, ctx.Token).ConfigureAwait(false);
                if (current == null)
                {
                    await ctx.Response.SendEvent(new ServerSentEvent { Id = (++sequence).ToString(CultureInfo.InvariantCulture), Event = "done", Data = "{\"deleted\":true}" }, true, ctx.Token).ConfigureAwait(false);
                    return;
                }

                string runJson = Serializer.Serialize(current);
                bool final = IsFinal(current.Status);
                bool runChanged = runJson != lastRun;
                lastRun = runJson;
                if (runChanged && !final)
                {
                    if (!await ctx.Response.SendEvent(new ServerSentEvent { Id = (++sequence).ToString(CultureInfo.InvariantCulture), Event = "run", Data = runJson }, false, ctx.Token).ConfigureAwait(false)) return;
                }

                List<RunStage> stages = (await Server.Database.RunStages.EnumerateAsync(new RunStageFilter { TenantId = run.TenantId, RunId = run.Id, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Items
                    .OrderBy(s => s.SortOrder).ToList();
                foreach (RunStage stage in stages)
                {
                    string json = Serializer.Serialize(stage);
                    if (lastStages.TryGetValue(stage.Stage, out string? previous) && previous == json) continue;
                    lastStages[stage.Stage] = json;
                    if (!await ctx.Response.SendEvent(new ServerSentEvent { Id = (++sequence).ToString(CultureInfo.InvariantCulture), Event = "stage", Data = json }, false, ctx.Token).ConfigureAwait(false)) return;
                }

                if (final)
                {
                    await ctx.Response.SendEvent(new ServerSentEvent { Id = (++sequence).ToString(CultureInfo.InvariantCulture), Event = "done", Data = runJson }, true, ctx.Token).ConfigureAwait(false);
                    return;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    await ctx.Response.SendEvent(new ServerSentEvent { Id = (++sequence).ToString(CultureInfo.InvariantCulture), Event = "timeout", Data = "{\"reconnect\":true}" }, true, ctx.Token).ConfigureAwait(false);
                    return;
                }

                if (DateTime.UtcNow >= nextPing)
                {
                    nextPing = DateTime.UtcNow.AddSeconds(15);
                    if (!await ctx.Response.SendEvent(new ServerSentEvent { Event = "ping", Data = "{}" }, false, ctx.Token).ConfigureAwait(false)) return;
                }

                try
                {
                    await Task.Delay(pollMs, ctx.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private static bool IsFinal(RunStatusEnum status)
        {
            return status == RunStatusEnum.Completed || status == RunStatusEnum.PartiallySucceeded || status == RunStatusEnum.Failed || status == RunStatusEnum.Cancelled || status == RunStatusEnum.Skipped;
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
