namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// Stage 9: research.score judges each new candidate; the ranker combines the judgment with feedback, novelty, source,
    /// and recency (plan section 17) into a composite score and tier. Findings are saved with their evidence links,
    /// ranked, and capped at MaxFindingsPerRun; duplicates are saved unscored for the record.
    /// </summary>
    public class ScoreStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Score"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return true; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            Settings.PipelineSettings p = services.Settings.Pipeline;
            List<string> preferred = ctx.Constraints.Where(c => c.Kind == ConstraintKindEnum.PreferredDomain).Select(c => c.Value).ToList();
            List<string> blocked = ctx.Constraints.Where(c => c.Kind == ConstraintKindEnum.BlockedDomain).Select(c => c.Value).ToList();
            int threshold = ctx.Topic.DisplayThreshold ?? p.DisplayThreshold;
            List<Candidate> fresh = ctx.Candidates.Where(c => !c.IsDuplicate).ToList();
            List<Finding> scored = new List<Finding>();
            object sync = new object();
            int fallbacks = 0;
            FeedbackProfile profile = await FeedbackProfile.LoadAsync(services, ctx, token).ConfigureAwait(false);

            using (SemaphoreSlim gate = new SemaphoreSlim(Math.Max(1, p.MaxConcurrentModelCalls)))
            {
                await Task.WhenAll(fresh.Select(async c =>
                {
                    await gate.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        double llm;
                        double confidence;
                        string rationale;
                        try
                        {
                            StructuredCallResult<ScoringResult> s = await services.CallAsync<ScoringResult>(ctx, "research.score", new Dictionary<string, object?>
                            {
                                { "candidate.title", c.Extracted.Title },
                                { "candidate.summary", c.Extracted.Summary + (String.IsNullOrWhiteSpace(c.Extracted.WhyItMatters) ? String.Empty : " Why it matters: " + c.Extracted.WhyItMatters) },
                                { "candidate.source", (c.Source.Domain ?? c.Source.CanonicalUrl) + (String.IsNullOrWhiteSpace(c.Source.Title) ? String.Empty : " (" + c.Source.Title + ")") },
                                { "candidate.dimension", c.Dimension?.Name ?? c.Extracted.Dimension }
                            }, token).ConfigureAwait(false);
                            llm = s.Value.Score;
                            confidence = s.Value.Confidence;
                            rationale = s.Value.Rationale;
                        }
                        catch (ModelCallException ex)
                        {
                            Interlocked.Increment(ref fallbacks);
                            llm = 50;
                            confidence = 0;
                            rationale = "The scoring model was unavailable (" + ex.Message + "); a neutral judgment was used.";
                        }

                        string url = c.Source.CanonicalUrl;
                        double feedback = await profile.ScoreAsync(services, ctx, c, token).ConfigureAwait(false);
                        double novelty = Ranker.Novelty(c.MaxSimilarity, ctx.Topic.NoveltyPreference);
                        double source = profile.SourceScore(c.Source.Domain, preferred.Any(d => UrlCanonicalizer.IsInDomain(url, d)), blocked.Any(d => UrlCanonicalizer.IsInDomain(url, d)));
                        double recency = Ranker.Recency(c.Source.PublishedUtc, ctx.NowUtc, p.RecencyHalfLifeHours);
                        double composite = Ranker.Composite(llm, feedback, novelty, source, recency,
                            ctx.Topic.WeightLlm ?? p.WeightLlm, ctx.Topic.WeightFeedback ?? p.WeightFeedback, ctx.Topic.WeightNovelty ?? p.WeightNovelty,
                            ctx.Topic.WeightSource ?? p.WeightSource, ctx.Topic.WeightRecency ?? p.WeightRecency);
                        composite = Ranker.Gate(composite, llm, p.RelevanceGateScore);
                        Finding f = Build(ctx, c);
                        f.LlmScore = llm;
                        f.FeedbackScore = feedback;
                        f.NoveltyScore = novelty;
                        f.SourceScore = source;
                        f.RecencyScore = recency;
                        f.RelevanceScore = composite;
                        f.Tier = Ranker.Tier(composite, threshold);
                        f.Confidence = confidence;
                        f.RelevanceRationale = rationale;
                        f.IsUpdateOfFindingId = c.UpdateOfFindingId;
                        c.Saved = f;
                        lock (sync) scored.Add(f);
                    }
                    finally
                    {
                        gate.Release();
                    }
                })).ConfigureAwait(false);
            }

            List<Finding> kept = scored.OrderByDescending(f => f.RelevanceScore).ThenBy(f => f.Title, StringComparer.Ordinal).Take(p.MaxFindingsPerRun).ToList();
            for (int i = 0; i < kept.Count; i++) kept[i].Rank = i + 1;
            foreach (Candidate c in fresh.Where(c => c.Saved != null && kept.Contains(c.Saved)))
            {
                c.Saved = await SaveAsync(ctx, services, c, c.Saved!, token).ConfigureAwait(false);
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.Findings, "tier", c.Saved.Tier.ToString());
            }

            foreach (Candidate c in ctx.Candidates.Where(c => c.IsDuplicate))
            {
                Finding d = Build(ctx, c);
                d.IsDuplicate = true;
                d.DuplicateOfFindingId = c.DuplicateOfFindingId;
                d.Tier = FindingTierEnum.BelowThreshold;
                d.RelevanceRationale = "Duplicate of an earlier finding.";
                c.Saved = await SaveAsync(ctx, services, c, d, token).ConfigureAwait(false);
            }

            ctx.Findings = fresh.Where(c => c.Saved != null && !c.Saved.IsDuplicate && kept.Any(k => k.Id == c.Saved.Id)).Select(c => c.Saved!).OrderBy(f => f.Rank).ToList();
            ctx.Run.NewFindingCount = ctx.Findings.Count;
            ctx.Run.DuplicateCount = ctx.Candidates.Count(c => c.IsDuplicate);
            string? note = fallbacks > 0 ? fallbacks + " findings were scored with a neutral judgment because the scoring model was unavailable." : null;
            if (note != null) ctx.Notes.Add(note);
            return StageOutcome.Of(ctx.Candidates.Count, ctx.Findings.Count, note);
        }

        private static Finding Build(RunContext ctx, Candidate c)
        {
            return new Finding
            {
                TenantId = ctx.Run.TenantId,
                RunId = ctx.Run.Id,
                TopicId = ctx.Topic.Id,
                UserId = ctx.Topic.UserId,
                DimensionId = c.Dimension?.Id,
                SourceItemId = c.Source.Id,
                Title = c.Extracted.Title.Trim(),
                Summary = c.Extracted.Summary,
                WhyItMatters = c.Extracted.WhyItMatters,
                Category = c.Extracted.Category,
                PublishedUtc = c.Source.PublishedUtc,
                IsDryRun = ctx.Run.IsDryRun
            };
        }

        private static async Task<Finding> SaveAsync(RunContext ctx, PipelineServices services, Candidate c, Finding f, CancellationToken token)
        {
            Finding saved = await services.Database.Findings.CreateAsync(f, token).ConfigureAwait(false);
            int order = 0;
            foreach (Evidence e in c.Evidence)
                await services.Database.FindingEvidence.CreateAsync(new FindingEvidence { TenantId = ctx.Run.TenantId, FindingId = saved.Id, EvidenceId = e.Id, Role = EvidenceRoleEnum.Supports, SortOrder = order++ }, token).ConfigureAwait(false);
            if (!saved.IsDuplicate && !ctx.Run.IsDryRun && c.Vector != null && ctx.EmbeddingModel != null)
                await services.Database.Embeddings.UpsertAsync(ctx.Run.TenantId, "Finding", saved.Id, ctx.Topic.Id, ctx.EmbeddingEndpointId, ctx.EmbeddingModel, c.Vector, null, token).ConfigureAwait(false);
            return saved;
        }
    }
}
