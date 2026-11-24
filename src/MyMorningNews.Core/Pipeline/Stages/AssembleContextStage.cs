namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Services;

    /// <summary>
    /// Stage 1: loads the topic context (dimensions, constraints, recent summaries, open predictions) and builds the prompt
    /// variables every later stage uses. Already seen URLs are looked up by the Filter stage, once the run's search results
    /// are known.
    /// </summary>
    public class AssembleContextStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "AssembleContext"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return true; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            string tenant = ctx.Run.TenantId;
            ctx.Dimensions = (await services.Database.TopicDimensions.EnumerateAsync(new TopicDimensionFilter { TenantId = tenant, TopicId = ctx.Topic.Id, PageSize = 500 }, token).ConfigureAwait(false)).Objects.OrderBy(d => d.SortOrder).ToList();
            ctx.Constraints = (await services.Database.TopicConstraints.EnumerateAsync(new TopicConstraintFilter { TenantId = tenant, TopicId = ctx.Topic.Id, PageSize = 500 }, token).ConfigureAwait(false)).Objects;
            ctx.Providers = await services.Search.ProvidersForTopicAsync(tenant, ctx.Topic.Id, token).ConfigureAwait(false);
            ctx.SinceUtc = ctx.Topic.LastRunUtc;

            RunHistorySnapshot history = await new MyMorningNews.Core.Briefings.HistoryContextService(services.Database).ResolveAsync(ctx.Topic, ctx.Run, token).ConfigureAwait(false);
            ctx.RecentSummaries = String.IsNullOrEmpty(history.ContextText) ? new List<string>() : new List<string> { history.ContextText };
            ctx.CompareHistory = history.Mode != HistoryModeEnum.Off.ToString();
            ctx.SinceUtc = ctx.CompareHistory ? ctx.Topic.LastRunUtc : null;
            ctx.HistoryInstructions = MyMorningNews.Core.Briefings.HistoryContextService.Prompt(history);

            List<string> dims = ctx.Dimensions.Select(RefinementService.FormatDimension).ToList();
            List<string> constraints = ctx.Constraints.Where(c => c.Kind != ConstraintKindEnum.TrackedQuestion && c.Kind != ConstraintKindEnum.FeedUrl && c.Kind != ConstraintKindEnum.SeedUrl)
                .GroupBy(c => c.Kind).Select(g => g.Key + ": " + String.Join(", ", g.Select(c => c.Value))).ToList();
            List<string> tracked = ctx.Constraints.Where(c => c.Kind == ConstraintKindEnum.TrackedQuestion).Select(c => c.Value).ToList();
            ctx.BaseValues = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "topic.name", ctx.Topic.Name },
                { "topic.brief", ctx.Topic.Brief ?? ctx.Topic.Description },
                { "topic.dimensions", dims },
                { "topic.constraints", constraints },
                { "topic.trackedQuestions", tracked },
                { "preferences.active", services.Learning == null ? new List<string>() : await services.Learning.ActivePreferencesAsync(tenant, ctx.Topic.UserId, ctx.Topic.Id, token).ConfigureAwait(false) },
                { "feedback.examples", services.Learning == null ? new List<string>() : await services.Learning.ExemplarsAsync(tenant, ctx.Topic.UserId, ctx.Topic.Id, token).ConfigureAwait(false) },
                { "history.recent", ctx.RecentSummaries },
                { "date.today", ctx.NowUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                { "since", ctx.SinceUtc.HasValue ? ctx.SinceUtc.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null }
            };

            string snapshot = ctx.Topic.Brief + "|" + String.Join("|", dims) + "|" + String.Join("|", constraints) + "|" + String.Join("|", tracked) + "|" + history.ContextHash;
            ctx.Run.ContextSnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant();
            if (ctx.Providers.Count == 0) throw new InvalidOperationException("The topic has no search providers. Add one or mark a provider as a default.");
            return StageOutcome.Of(ctx.Dimensions.Count + ctx.Constraints.Count, ctx.Providers.Count);
        }
    }
}
