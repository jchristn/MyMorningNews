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
    using MyMorningNews.Core.Requests;

    /// <summary>
    /// Stage 5: fetches selected results in parallel, reusing a tenant's stored source when it was fetched in the last
    /// seven days. Fails when results were selected and none could be read.
    /// </summary>
    public class FetchStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Fetch"; }
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
            object sync = new object();
            using (SemaphoreSlim gate = new SemaphoreSlim(services.Settings.Pipeline.MaxParallelFetches))
            {
                await Task.WhenAll(ctx.Selected.Select(async r =>
                {
                    await gate.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        SourceItem? source = await ReadOrFetchAsync(ctx, services, r, blocked, token).ConfigureAwait(false);
                        if (source != null) lock (sync) ctx.Sources.Add(source);
                    }
                    finally
                    {
                        gate.Release();
                    }
                })).ConfigureAwait(false);
            }

            if (ctx.Selected.Count > 0 && ctx.Sources.Count == 0) throw new InvalidOperationException("None of the " + ctx.Selected.Count + " selected pages could be read.");
            ctx.Run.SourceCount = ctx.Sources.Count;
            return StageOutcome.Of(ctx.Selected.Count, ctx.Sources.Count);
        }

        private static async Task<SourceItem?> ReadOrFetchAsync(RunContext ctx, PipelineServices services, SearchResult r, List<string> blocked, CancellationToken token)
        {
            SourceItem? existing = (await services.Database.SourceItems.EnumerateAsync(new SourceItemFilter { TenantId = ctx.Run.TenantId, CanonicalUrlHash = r.CanonicalUrlHash, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault();
            if (existing != null && !String.IsNullOrWhiteSpace(existing.ContentText) && existing.FetchedUtc > ctx.NowUtc.AddDays(-7))
            {
                existing.TimesSeen++;
                existing = await services.Database.SourceItems.UpdateAsync(existing, token).ConfigureAwait(false);
                await SetAsync(services, r, SearchResultDispositionEnum.Fetched, existing.Id, token).ConfigureAwait(false);
                return existing;
            }

            FetchResult fetched = await services.Fetch.FetchAsync(r.Url, blocked, token).ConfigureAwait(false);
            if (fetched.Disposition != FetchDispositionEnum.Fetched || String.IsNullOrWhiteSpace(fetched.Text))
            {
                SearchResultDispositionEnum d = fetched.Disposition == FetchDispositionEnum.RobotsDenied ? SearchResultDispositionEnum.SkippedRobots
                    : fetched.Disposition == FetchDispositionEnum.Blocked ? SearchResultDispositionEnum.SkippedBlocked : SearchResultDispositionEnum.FetchFailed;
                await SetAsync(services, r, d, null, token).ConfigureAwait(false);
                return null;
            }

            SourceItem item = existing ?? new SourceItem
            {
                TenantId = ctx.Run.TenantId,
                CanonicalUrlHash = r.CanonicalUrlHash,
                FirstSeenRunId = ctx.Run.Id,
                TimesSeen = 0
            };
            item.CanonicalUrl = fetched.CanonicalUrl ?? r.Url;
            item.Domain = UrlCanonicalizer.Domain(fetched.FinalUrl ?? r.Url);
            item.Title = Clean(!String.IsNullOrWhiteSpace(fetched.Title) ? fetched.Title : r.Title);
            item.PublishedUtc = r.PublishedUtc ?? item.PublishedUtc;
            item.FetchedUtc = DateTime.UtcNow;
            item.HttpStatus = fetched.StatusCode;
            item.ContentType = fetched.ContentType != null && fetched.ContentType.Length > 128 ? fetched.ContentType.Substring(0, 128) : fetched.ContentType;
            item.ContentHash = fetched.ContentHash;
            item.ContentText = Clean(fetched.Text);
            item.ContentBytes = fetched.Bytes;
            item.TimesSeen++;
            item = existing == null ? await services.Database.SourceItems.CreateAsync(item, token).ConfigureAwait(false) : await services.Database.SourceItems.UpdateAsync(item, token).ConfigureAwait(false);
            await SetAsync(services, r, SearchResultDispositionEnum.Fetched, item.Id, token).ConfigureAwait(false);
            return item;
        }

        private static async Task SetAsync(PipelineServices services, SearchResult r, SearchResultDispositionEnum d, string? sourceId, CancellationToken token)
        {
            r.Disposition = d;
            r.SourceItemId = sourceId;
            await services.Database.SearchResults.UpdateAsync(r, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Remove NUL and other control characters (except tab, newline, and carriage return) that Postgres text columns reject.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Clean text, or null.</returns>
        public static string? Clean(string? text)
        {
            if (text == null) return null;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text) if (c >= ' ' || c == '\t' || c == '\n' || c == '\r') sb.Append(c);
            return sb.ToString();
        }
    }
}
