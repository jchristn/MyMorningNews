namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Stage 4: drops duplicates within the run, blocked domains, and URLs already fetched for the topic, then keeps the
    /// best results up to the fetch limit (preferred domains first, then provider rank and recency). A URL counts as seen
    /// only when a non-dry run that finished (Completed or PartiallySucceeded) fetched it, so a failed, cancelled, or dry
    /// run never hides pages from later runs. The database is asked about exactly this run's URL hashes, so the answer
    /// does not depend on how much history the topic has.
    /// </summary>
    public class FilterStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Filter"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return true; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            List<string> blocked = ctx.Constraints.Where(c => c.Kind == ConstraintKindEnum.BlockedDomain).Select(c => c.Value).ToList();
            List<string> preferred = ctx.Constraints.Where(c => c.Kind == ConstraintKindEnum.PreferredDomain).Select(c => c.Value).ToList();
            ctx.SeenUrlHashes = await services.Database.SearchResults.SeenUrlHashesAsync(ctx.Run.TenantId, ctx.Topic.Id, ctx.Results.Select(r => r.CanonicalUrlHash), ctx.Run.Id, token).ConfigureAwait(false);
            HashSet<string> inRun = new HashSet<string>(StringComparer.Ordinal);
            List<SearchResult> eligible = new List<SearchResult>();
            foreach (SearchResult r in ctx.Results.OrderBy(r => r.Rank))
            {
                SearchResultDispositionEnum? d = null;
                if (!inRun.Add(r.CanonicalUrlHash)) d = SearchResultDispositionEnum.SkippedDuplicate;
                else if (blocked.Any(b => UrlCanonicalizer.IsInDomain(r.Url, b))) d = SearchResultDispositionEnum.SkippedBlocked;
                else if (ctx.SeenUrlHashes.Contains(r.CanonicalUrlHash)) d = SearchResultDispositionEnum.SkippedSeen;
                if (d.HasValue) await SetAsync(services, r, d.Value, token).ConfigureAwait(false);
                else eligible.Add(r);
            }

            List<SearchResult> ordered = eligible
                .OrderByDescending(r => preferred.Any(p => UrlCanonicalizer.IsInDomain(r.Url, p)))
                .ThenBy(r => r.Rank)
                .ThenByDescending(r => r.PublishedUtc ?? DateTime.MinValue)
                .ToList();
            int max = services.Settings.Pipeline.MaxFetchesPerRun;
            ctx.Selected = ordered.Take(max).ToList();
            foreach (SearchResult r in ordered.Skip(max)) await SetAsync(services, r, SearchResultDispositionEnum.SkippedLimit, token).ConfigureAwait(false);
            return StageOutcome.Of(ctx.Results.Count, ctx.Selected.Count);
        }

        private static async Task SetAsync(PipelineServices services, SearchResult r, SearchResultDispositionEnum d, CancellationToken token)
        {
            r.Disposition = d;
            await services.Database.SearchResults.UpdateAsync(r, token).ConfigureAwait(false);
        }
    }
}
