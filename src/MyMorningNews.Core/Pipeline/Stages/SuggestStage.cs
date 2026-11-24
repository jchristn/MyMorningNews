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
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Services;

    /// <summary>
    /// Stage 13: research.suggest writes up to five things to dig into, linked to findings, avoiding repeats of earlier
    /// suggestions (including dismissed and completed ones). Non-critical.
    /// </summary>
    public class SuggestStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Suggest"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return false; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            if (ctx.Findings.Count == 0) return StageOutcome.Of(0, 0);
            List<Suggestion> history = ctx.CompareHistory ? (await services.Database.Suggestions.EnumerateAsync(new SuggestionFilter { TenantId = ctx.Run.TenantId, TopicId = ctx.Topic.Id, IsDryRun = false, PageSize = 50 }, token).ConfigureAwait(false)).Objects : new List<Suggestion>();
            StructuredCallResult<SuggestionResult> result = await services.CallAsync<SuggestionResult>(ctx, "research.suggest", new Dictionary<string, object?>
            {
                { "findings", PipelineServices.FormatFindings(ctx.Findings, 25) },
                { "suggestions.history", history.Select(s => s.Title + " (" + s.Status + ")").ToList() }
            }, token).ConfigureAwait(false);

            int created = 0;
            foreach (ProposedSuggestion s in result.Value.Suggestions)
            {
                if (history.Any(h => PipelineServices.WordSimilarity(h.Title, s.Title) >= 0.75)) continue;
                Suggestion saved = await services.Database.Suggestions.CreateAsync(new Suggestion
                {
                    TenantId = ctx.Run.TenantId,
                    RunId = ctx.Run.Id,
                    TopicId = ctx.Topic.Id,
                    UserId = ctx.Topic.UserId,
                    Title = s.Title.Trim(),
                    Description = s.Description,
                    Rationale = s.Rationale,
                    Priority = s.Priority,
                    Effort = s.Effort,
                    Status = SuggestionStatusEnum.New,
                    FindingIds = s.Findings.Select(t => PipelineServices.MatchFinding(ctx.Findings, t)?.Id).Where(id => id != null).Select(id => id!).Distinct().ToList(),
                    IsDryRun = ctx.Run.IsDryRun
                }, token).ConfigureAwait(false);
                history.Add(saved);
                created++;
            }

            return StageOutcome.Of(ctx.Findings.Count, created);
        }
    }
}
