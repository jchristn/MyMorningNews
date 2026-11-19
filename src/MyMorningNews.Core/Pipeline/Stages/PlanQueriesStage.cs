namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Services;

    /// <summary>
    /// Stage 2: research.queries plans search queries across the dimensions. When the model is unavailable, the topic's
    /// seed queries are used so the run can still proceed.
    /// </summary>
    public class PlanQueriesStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "PlanQueries"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return true; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            int max = services.Settings.Pipeline.MaxQueriesPerRun;
            string? note = null;
            try
            {
                StructuredCallResult<QueryPlanResult> plan = await services.CallAsync<QueryPlanResult>(ctx, "research.queries",
                    new Dictionary<string, object?> { { "max.queries", max.ToString(CultureInfo.InvariantCulture) } }, token).ConfigureAwait(false);
                ctx.PlannedQueries = plan.Value.Queries.Take(max).ToList();
            }
            catch (ModelCallException ex)
            {
                if (ctx.Topic.SeedQueries.Count == 0) throw;
                note = "Query planning failed (" + ex.Message + "); used the topic's seed queries.";
                ctx.Notes.Add(note);
                ctx.PlannedQueries = ctx.Topic.SeedQueries.Take(max).Select(q => new PlannedQuery { Query = q, Rationale = "Seed query" }).ToList();
            }

            ctx.PlannedQueries = ctx.PlannedQueries.GroupBy(q => q.Query.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            return StageOutcome.Of(max, ctx.PlannedQueries.Count, note);
        }
    }
}
