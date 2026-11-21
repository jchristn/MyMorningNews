namespace MyMorningNews.Core.Responses
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Cost of one run stage.
    /// </summary>
    public class StageCost : CostTotals
    {
        /// <summary>
        /// Run stage.
        /// </summary>
        public string RunStageId { get; set; } = String.Empty;

        /// <summary>
        /// Stage name, for example Plan, Search, Extract, Score, Synthesize.
        /// </summary>
        public string Stage { get; set; } = String.Empty;

        /// <summary>
        /// Stage status.
        /// </summary>
        public StageStatusEnum Status { get; set; } = StageStatusEnum.Pending;

        /// <summary>
        /// Position in the run.
        /// </summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Stage duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Items into the stage.
        /// </summary>
        public int ItemsIn { get; set; } = 0;

        /// <summary>
        /// Items out of the stage.
        /// </summary>
        public int ItemsOut { get; set; } = 0;

        /// <summary>
        /// Share of the run's estimated cost, from 0 to 1. Null when the run has no priced cost.
        /// </summary>
        public double? ShareOfCost { get; set; } = null;
    }
}
