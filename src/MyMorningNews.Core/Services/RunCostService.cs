namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Builds run cost reports from the run's model calls: totals, each stage, each model, and each task role. Reports
    /// are computed on request, so they are current while a run is going and correct for runs from before stage tracking.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class RunCostService
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <exception cref="ArgumentNullException">database is null.</exception>
        public RunCostService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build a run's cost report.
        /// </summary>
        /// <param name="run">Run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Report.</returns>
        /// <exception cref="ArgumentNullException">run is null.</exception>
        public async Task<RunCostReport> BuildAsync(Run run, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(run);
            List<LlmCall> calls = new List<LlmCall>();
            for (int page = 1; page <= 100; page++)
            {
                EnumerationResult<LlmCall> batch = await _Database.LlmCalls.EnumerateAsync(new LlmCallFilter { TenantId = run.TenantId, RunId = run.Id, PageSize = 1000, PageNumber = page }, token).ConfigureAwait(false);
                calls.AddRange(batch.Items);
                if (batch.Items.Count < 1000) break;
            }

            List<RunStage> stages = (await _Database.RunStages.EnumerateAsync(new RunStageFilter { TenantId = run.TenantId, RunId = run.Id, PageSize = 100 }, token).ConfigureAwait(false))
                .Items.OrderBy(s => s.SortOrder).ThenBy(s => s.CreatedUtc).ToList();

            RunCostReport report = new RunCostReport { RunId = run.Id, TopicId = run.TopicId, Status = run.Status };
            Fill(report.Totals, calls);
            if (report.Totals.EstimatedCostUsd.HasValue && run.NewFindingCount > 0)
                report.CostPerNewFindingUsd = Math.Round(report.Totals.EstimatedCostUsd.Value / run.NewFindingCount, 6);

            HashSet<string> stageIds = new HashSet<string>(stages.Select(s => s.Id), StringComparer.Ordinal);
            foreach (RunStage s in stages)
            {
                StageCost cost = new StageCost
                {
                    RunStageId = s.Id,
                    Stage = s.Stage,
                    Status = s.Status,
                    SortOrder = s.SortOrder,
                    DurationMs = s.DurationMs,
                    ItemsIn = s.ItemsIn,
                    ItemsOut = s.ItemsOut
                };
                Fill(cost, calls.Where(c => String.Equals(c.RunStageId, s.Id, StringComparison.Ordinal)).ToList());
                if (report.Totals.EstimatedCostUsd.HasValue && report.Totals.EstimatedCostUsd.Value > 0)
                    cost.ShareOfCost = Math.Round((double)((cost.EstimatedCostUsd ?? 0) / report.Totals.EstimatedCostUsd.Value), 4);
                report.Stages.Add(cost);
            }

            List<LlmCall> loose = calls.Where(c => c.RunStageId == null || !stageIds.Contains(c.RunStageId)).ToList();
            if (loose.Count > 0)
            {
                report.Unattributed = new CostTotals();
                Fill(report.Unattributed, loose);
            }

            Dictionary<string, ModelEndpoint?> endpoints = new Dictionary<string, ModelEndpoint?>(StringComparer.Ordinal);
            foreach (IGrouping<string, LlmCall> group in calls.GroupBy(c => (c.ModelEndpointId ?? String.Empty) + "|" + (c.Model ?? String.Empty)))
            {
                LlmCall first = group.First();
                ModelEndpoint? endpoint = null;
                if (!String.IsNullOrEmpty(first.ModelEndpointId))
                {
                    if (!endpoints.TryGetValue(first.ModelEndpointId, out endpoint))
                    {
                        endpoint = await _Database.ModelEndpoints.ReadAsync(run.TenantId, first.ModelEndpointId, token).ConfigureAwait(false);
                        endpoints[first.ModelEndpointId] = endpoint;
                    }
                }

                ModelCost cost = new ModelCost { ModelEndpointId = endpoint?.Id, EndpointName = endpoint?.Name, Provider = first.Provider, Model = first.Model };
                Fill(cost, group.ToList());
                report.ByModel.Add(cost);
            }

            foreach (IGrouping<Enums.ModelTaskEnum, LlmCall> group in calls.GroupBy(c => c.Task))
            {
                TaskCost cost = new TaskCost { Task = group.Key };
                Fill(cost, group.ToList());
                report.ByTask.Add(cost);
            }

            report.ByModel = report.ByModel.OrderByDescending(m => m.EstimatedCostUsd ?? -1).ThenByDescending(m => m.InputTokens + m.OutputTokens).ToList();
            report.ByTask = report.ByTask.OrderByDescending(t => t.EstimatedCostUsd ?? -1).ThenByDescending(t => t.InputTokens + t.OutputTokens).ToList();
            return report;
        }

        #endregion

        #region Private-Methods

        private static void Fill(CostTotals totals, List<LlmCall> calls)
        {
            totals.LlmCallCount = calls.Count;
            totals.UnpricedCallCount = calls.Count(c => !c.EstimatedCostUsd.HasValue);
            totals.FailedCallCount = calls.Count(c => !c.Success);
            totals.InputTokens = calls.Sum(c => c.InputTokens);
            totals.OutputTokens = calls.Sum(c => c.OutputTokens);
            totals.CachedInputTokens = calls.Sum(c => c.CachedInputTokens);
            totals.CacheWriteTokens = calls.Sum(c => c.CacheWriteTokens);
            totals.ReasoningTokens = calls.Sum(c => c.ReasoningTokens);
            totals.ModelDurationMs = Math.Round(calls.Sum(c => c.DurationMs), 1);
            totals.EstimatedCostUsd = calls.Any(c => c.EstimatedCostUsd.HasValue) ? calls.Sum(c => c.EstimatedCostUsd ?? 0) : null;
            totals.InputCostUsd = calls.Any(c => c.InputCostUsd.HasValue) ? calls.Sum(c => c.InputCostUsd ?? 0) : null;
            totals.OutputCostUsd = calls.Any(c => c.OutputCostUsd.HasValue) ? calls.Sum(c => c.OutputCostUsd ?? 0) : null;
        }

        #endregion
    }
}
