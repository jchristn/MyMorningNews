namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// What a run cost: totals, each stage, each model, and each task role. Computed from the run's model calls, so it is
    /// current while the run is still going.
    /// </summary>
    public class RunCostReport
    {
        /// <summary>
        /// Run.
        /// </summary>
        public string RunId { get; set; } = String.Empty;

        /// <summary>
        /// Topic.
        /// </summary>
        public string TopicId { get; set; } = String.Empty;

        /// <summary>
        /// Run status.
        /// </summary>
        public RunStatusEnum Status { get; set; } = RunStatusEnum.Queued;

        /// <summary>
        /// Currency of every amount. Always USD.
        /// </summary>
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Run totals.
        /// </summary>
        public CostTotals Totals { get; set; } = new CostTotals();

        /// <summary>
        /// Estimated cost per new finding, when the run found any and had a priced cost.
        /// </summary>
        public decimal? CostPerNewFindingUsd { get; set; } = null;

        /// <summary>
        /// Stages in run order.
        /// </summary>
        public List<StageCost> Stages { get; set; } = new List<StageCost>();

        /// <summary>
        /// Calls made for the run outside any stage (rare; for example calls logged before stage tracking existed).
        /// </summary>
        public CostTotals? Unattributed { get; set; } = null;

        /// <summary>
        /// By model, most expensive first.
        /// </summary>
        public List<ModelCost> ByModel { get; set; } = new List<ModelCost>();

        /// <summary>
        /// By task role, most expensive first.
        /// </summary>
        public List<TaskCost> ByTask { get; set; } = new List<TaskCost>();
    }
}
