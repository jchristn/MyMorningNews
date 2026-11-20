namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Search;

    /// <summary>
    /// Stage 3: runs every planned query against every search provider (RSS once, over the topic's feeds), recording a
    /// searchqueries row per call and a searchresults row per hit. Fails only when every provider call failed.
    /// </summary>
    public class SearchStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Search"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return true; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            if (ctx.Topic.AgenticSearch)
            {
                string? fallback = await AgenticSearch.TryRunAsync(ctx, services, token).ConfigureAwait(false);
                if (fallback == null) return StageOutcome.Of(ctx.PlannedQueries.Count, ctx.Results.Count, "Agentic search: " + ctx.Results.Count + " pages picked.");
                ctx.Notes.Add(fallback);
                services.Logging.Info("[SearchStage] run " + ctx.Run.Id + ": " + fallback);
            }

            List<string> feeds = ctx.Constraints.Where(c => c.Kind == ConstraintKindEnum.FeedUrl).Select(c => c.Value).ToList();
            List<string> include = ctx.Constraints.Where(c => c.Kind == ConstraintKindEnum.IncludeKeyword).Select(c => c.Value).ToList();
            List<string> blocked = ctx.Constraints.Where(c => c.Kind == ConstraintKindEnum.BlockedDomain).Select(c => c.Value).ToList();
            List<SearchCall> calls = new List<SearchCall>();
            foreach (SearchProvider p in ctx.Providers)
            {
                if (p.Kind == SearchProviderKindEnum.Rss)
                {
                    if (feeds.Count > 0) calls.Add(new SearchCall { Provider = p, Query = new PlannedQuery { Query = String.Join(" ", new[] { ctx.Topic.Name }.Concat(include).Concat(ctx.PlannedQueries.Select(q => q.Query))), Rationale = "Topic feeds" } });
                    continue;
                }

                foreach (PlannedQuery q in ctx.PlannedQueries) calls.Add(new SearchCall { Provider = p, Query = q });
            }

            int succeeded = 0;
            object gateLock = new object();
            using (SemaphoreSlim gate = new SemaphoreSlim(4))
            {
                await Task.WhenAll(calls.Select(async call =>
                {
                    await gate.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        SearchOutcome outcome = await services.Search.SearchAsync(call.Provider, new ResearchSearchRequest
                        {
                            Query = call.Query.Query,
                            MaxResults = services.Settings.Pipeline.MaxResultsPerQuery,
                            ExcludeDomains = blocked,
                            FeedUrls = call.Provider.Kind == SearchProviderKindEnum.Rss ? feeds : new List<string>(),
                            SinceUtc = call.Provider.Kind == SearchProviderKindEnum.Rss ? ctx.SinceUtc : null
                        }, token).ConfigureAwait(false);
                        TopicDimension? dim = call.Query.Dimension == null ? null : ctx.Dimensions.FirstOrDefault(d => String.Equals(d.Name, call.Query.Dimension.Trim(), StringComparison.OrdinalIgnoreCase));
                        SearchQuery row = await services.Database.SearchQueries.CreateAsync(new SearchQuery
                        {
                            TenantId = ctx.Run.TenantId,
                            RunId = ctx.Run.Id,
                            TopicId = ctx.Topic.Id,
                            DimensionId = dim?.Id,
                            QueryText = call.Query.Query.Length > 2000 ? call.Query.Query.Substring(0, 2000) : call.Query.Query,
                            Rationale = call.Query.Rationale,
                            SearchProviderId = call.Provider.Id,
                            Provider = call.Provider.Kind.ToString(),
                            ResultCount = outcome.Hits.Count,
                            DurationMs = outcome.DurationMs,
                            Succeeded = outcome.Success,
                            ErrorMessage = outcome.Error
                        }, token).ConfigureAwait(false);
                        if (outcome.Success) Interlocked.Increment(ref succeeded);
                        foreach (SearchHit h in outcome.Hits)
                        {
                            SearchResult result = await services.Database.SearchResults.CreateAsync(new SearchResult
                            {
                                TenantId = ctx.Run.TenantId,
                                SearchQueryId = row.Id,
                                RunId = ctx.Run.Id,
                                TopicId = ctx.Topic.Id,
                                Url = h.Url,
                                CanonicalUrlHash = Fetch.UrlCanonicalizer.Hash(h.Url) ?? h.CanonicalUrl,
                                Title = h.Title,
                                Snippet = h.Snippet,
                                PublishedUtc = h.PublishedUtc,
                                Rank = h.Rank,
                                Provider = h.Provider
                            }, token).ConfigureAwait(false);
                            lock (gateLock) ctx.Results.Add(result);
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                })).ConfigureAwait(false);
            }

            if (calls.Count > 0 && succeeded == 0) throw new InvalidOperationException("Every search provider failed; see the run's queries for the errors.");
            return StageOutcome.Of(calls.Count, ctx.Results.Count);
        }
    }
}
