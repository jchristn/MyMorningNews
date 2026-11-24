namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Services;

    /// <summary>
    /// Stage 8: marks candidates that repeat an earlier finding (or another candidate in this run). With embeddings,
    /// similarity at or above DuplicateSimilarity is a duplicate and the band from BorderlineSimilarity is judged by
    /// research.dedupe-judge (Duplicate, Update, or Different). Without embeddings, word similarity of titles is used.
    /// Also records the highest similarity for the novelty score. Non-critical.
    /// Earlier findings are those inside Pipeline.DedupeLookbackDays that are not from a dry run and not themselves
    /// duplicates. With embeddings the nearest ones come from a vector search over that whole window and are read by
    /// identifier. Without embeddings, exact title repeats are found by an indexed query over the whole window, and
    /// word overlap is compared against the topic's most recent Pipeline.DedupeTextHistoryLimit findings.
    /// </summary>
    public class DedupeStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Dedupe"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return false; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            if (ctx.Candidates.Count == 0) return StageOutcome.Of(0, 0);
            double dup = services.Settings.Pipeline.DuplicateSimilarity;
            double border = Math.Min(dup, services.Settings.Pipeline.BorderlineSimilarity);
            DateTime dedupeSince = ctx.NowUtc.AddDays(-services.Settings.Pipeline.DedupeLookbackDays);
            DateTime noveltySince = ctx.NowUtc.AddDays(-services.Settings.Pipeline.NoveltyLookbackDays);
            List<Finding>? recent = null;

            for (int i = 0; i < ctx.Candidates.Count; i++)
            {
                Candidate c = ctx.Candidates[i];
                for (int j = 0; j < i; j++)
                {
                    Candidate earlier = ctx.Candidates[j];
                    if (earlier.IsDuplicate) continue;
                    double sim = c.Vector != null && earlier.Vector != null ? Cosine(c.Vector, earlier.Vector) : PipelineServices.WordSimilarity(c.Extracted.Title, earlier.Extracted.Title);
                    if (sim >= dup || (c.Vector == null && sim >= 0.8)) { c.IsDuplicate = true; break; }
                }

                if (c.IsDuplicate || !ctx.CompareHistory) continue;
                if (c.Vector != null && ctx.EmbeddingModel != null)
                {
                    List<EmbeddingMatch> near = await services.Database.Embeddings.NearestAsync(ctx.Run.TenantId, ctx.Topic.Id, "Finding", ctx.EmbeddingModel, c.Vector, dedupeSince, null, 5, token).ConfigureAwait(false);
                    Dictionary<string, Finding> byId = (await services.Database.Findings.ReadManyAsync(ctx.Run.TenantId, near.Select(m => m.OwnerId), token).ConfigureAwait(false))
                        .Where(f => f.TopicId == ctx.Topic.Id && !f.IsDryRun && !f.IsDuplicate && f.CreatedUtc >= dedupeSince && f.RunId != ctx.Run.Id)
                        .ToDictionary(f => f.Id, f => f, StringComparer.Ordinal);
                    EmbeddingMatch? best = near.FirstOrDefault();
                    c.MaxSimilarity = near.Where(m => byId.TryGetValue(m.OwnerId, out Finding? f) && f.CreatedUtc >= noveltySince).Select(m => (double?)m.Similarity).DefaultIfEmpty(null).Max();
                    if (best == null || !byId.TryGetValue(best.OwnerId, out Finding? prior)) continue;
                    if (best.Similarity >= dup)
                    {
                        c.IsDuplicate = true;
                        c.DuplicateOfFindingId = prior.Id;
                    }
                    else if (best.Similarity >= border)
                    {
                        await JudgeAsync(ctx, services, c, prior, token).ConfigureAwait(false);
                    }
                }
                else
                {
                    if (recent == null) recent = await TextHistoryAsync(ctx, services, dedupeSince, token).ConfigureAwait(false);
                    Finding? prior = recent.Select(f => new { f, sim = PipelineServices.WordSimilarity(c.Extracted.Title, f.Title) }).Where(x => x.sim >= 0.8).OrderByDescending(x => x.sim).Select(x => x.f).FirstOrDefault();
                    if (prior != null)
                    {
                        c.IsDuplicate = true;
                        c.DuplicateOfFindingId = prior.Id;
                        continue;
                    }

                    // A reworded follow-up shares the story's words without matching the title: let the model judge it
                    // against the closest earlier finding, as the embeddings path does for borderline matches.
                    double gate = services.Settings.Pipeline.TextUpdateSimilarity;
                    Finding? closest = null;
                    double closestSim = gate;
                    foreach (Finding f in recent)
                    {
                        double sim = 0.5 * PipelineServices.WordSimilarity(c.Extracted.Title, f.Title) + 0.5 * PipelineServices.WordSimilarity(c.Extracted.Summary, f.Summary);
                        if (sim >= closestSim)
                        {
                            closest = f;
                            closestSim = sim;
                        }
                    }

                    if (closest != null) await JudgeAsync(ctx, services, c, closest, token).ConfigureAwait(false);
                }
            }

            return StageOutcome.Of(ctx.Candidates.Count, ctx.Candidates.Count(c => !c.IsDuplicate));
        }

        /// <summary>
        /// Cosine similarity of two vectors of equal length.
        /// </summary>
        /// <param name="a">First vector.</param>
        /// <param name="b">Second vector.</param>
        /// <returns>Similarity, -1 to 1; 0 when lengths differ.</returns>
        public static double Cosine(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0) return 0;
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }

            return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        private static async Task<List<Finding>> TextHistoryAsync(RunContext ctx, PipelineServices services, DateTime dedupeSince, CancellationToken token)
        {
            // Loaded once per run, and only when a candidate has no vector. Exact title repeats first, from the whole
            // lookback window, then the bounded recent history for word overlap.
            List<Finding> history = await services.Database.Findings.DedupeTitleMatchesAsync(ctx.Run.TenantId, ctx.Topic.Id, ctx.Candidates.Where(c => c.Vector == null || ctx.EmbeddingModel == null).Select(c => c.Extracted.Title), dedupeSince, ctx.Run.Id, token).ConfigureAwait(false);
            HashSet<string> ids = new HashSet<string>(history.Select(f => f.Id), StringComparer.Ordinal);
            foreach (Finding f in await services.Database.Findings.DedupeHistoryAsync(ctx.Run.TenantId, ctx.Topic.Id, dedupeSince, ctx.Run.Id, services.Settings.Pipeline.DedupeTextHistoryLimit, token).ConfigureAwait(false))
                if (ids.Add(f.Id)) history.Add(f);
            return history;
        }

        private static async Task JudgeAsync(RunContext ctx, PipelineServices services, Candidate c, Finding prior, CancellationToken token)
        {
            try
            {
                StructuredCallResult<DedupeJudgeResult> verdict = await services.CallAsync<DedupeJudgeResult>(ctx, "research.dedupe-judge", new Dictionary<string, object?>
                {
                    { "candidate.title", c.Extracted.Title },
                    { "candidate.summary", c.Extracted.Summary },
                    { "existing.title", prior.Title },
                    { "existing.summary", prior.Summary },
                    { "existing.date", prior.CreatedUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }
                }, token).ConfigureAwait(false);
                if (verdict.Value.Verdict == DedupeVerdictEnum.Duplicate)
                {
                    c.IsDuplicate = true;
                    c.DuplicateOfFindingId = prior.Id;
                }
                else if (verdict.Value.Verdict == DedupeVerdictEnum.Update)
                {
                    c.UpdateOfFindingId = prior.Id;
                }
            }
            catch (ModelCallException)
            {
                c.UpdateOfFindingId = prior.Id;
            }
        }
    }
}
