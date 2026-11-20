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
    /// Stage 1: loads the topic context (dimensions, constraints, recent summaries, already seen URLs, open predictions) and
    /// builds the prompt variables every later stage uses. A URL counts as seen only when a non-dry run that finished
    /// (Completed or PartiallySucceeded) fetched it, so a failed, cancelled, or dry run never hides pages from later runs.
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
            ctx.Dimensions = (await services.Database.TopicDimensions.EnumerateAsync(new TopicDimensionFilter { TenantId = tenant, TopicId = ctx.Topic.Id, PageSize = 500 }, token).ConfigureAwait(false)).Items.OrderBy(d => d.SortOrder).ToList();
            ctx.Constraints = (await services.Database.TopicConstraints.EnumerateAsync(new TopicConstraintFilter { TenantId = tenant, TopicId = ctx.Topic.Id, PageSize = 500 }, token).ConfigureAwait(false)).Items;
            ctx.Providers = await services.Search.ProvidersForTopicAsync(tenant, ctx.Topic.Id, token).ConfigureAwait(false);
            ctx.SinceUtc = ctx.Topic.LastRunUtc;

            List<Run> recent = (await services.Database.Runs.EnumerateAsync(new RunFilter { TenantId = tenant, TopicId = ctx.Topic.Id, Status = RunStatusEnum.Completed, PageSize = 5 }, token).ConfigureAwait(false)).Items;
            List<Run> partial = (await services.Database.Runs.EnumerateAsync(new RunFilter { TenantId = tenant, TopicId = ctx.Topic.Id, Status = RunStatusEnum.PartiallySucceeded, PageSize = 5 }, token).ConfigureAwait(false)).Items;
            ctx.RecentSummaries = recent.Concat(partial).Where(r => !r.IsDryRun && !String.IsNullOrWhiteSpace(r.Summary) && r.Id != ctx.Run.Id)
                .OrderByDescending(r => r.CompletedUtc).Take(5).Select(r => (r.CompletedUtc ?? r.QueuedUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ": " + r.Summary).ToList();

            List<SearchResult> seen = (await services.Database.SearchResults.EnumerateAsync(new SearchResultFilter { TenantId = tenant, TopicId = ctx.Topic.Id, Disposition = SearchResultDispositionEnum.Fetched, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            HashSet<string> finished = new HashSet<string>(StringComparer.Ordinal);
            foreach (RunStatusEnum status in new[] { RunStatusEnum.Completed, RunStatusEnum.PartiallySucceeded })
                foreach (Run r in (await services.Database.Runs.EnumerateAsync(new RunFilter { TenantId = tenant, TopicId = ctx.Topic.Id, Status = status, IsDryRun = false, PageSize = 1000 }, token).ConfigureAwait(false)).Items)
                    finished.Add(r.Id);
            ctx.SeenUrlHashes = new HashSet<string>(seen.Where(r => r.RunId != ctx.Run.Id && finished.Contains(r.RunId)).Select(r => r.CanonicalUrlHash), StringComparer.Ordinal);

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

            string snapshot = ctx.Topic.Brief + "|" + String.Join("|", dims) + "|" + String.Join("|", constraints) + "|" + String.Join("|", tracked);
            ctx.Run.ContextSnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant();
            if (ctx.Providers.Count == 0) throw new InvalidOperationException("The topic has no search providers. Add one or mark a provider as a default.");
            return StageOutcome.Of(ctx.Dimensions.Count + ctx.Constraints.Count, ctx.Providers.Count);
        }
    }
}
