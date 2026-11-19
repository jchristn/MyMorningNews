namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Prediction accuracy (plan section 20).
    /// </summary>
    public class AccuracyReport
    {
        /// <summary>
        /// Predictions.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Open.
        /// </summary>
        public int Open { get; set; } = 0;

        /// <summary>
        /// Open past their horizon, or with a proposed resolution.
        /// </summary>
        public int DueForReview { get; set; } = 0;

        /// <summary>
        /// Resolved as accurate, partially accurate, or inaccurate.
        /// </summary>
        public int Resolved { get; set; } = 0;

        /// <summary>
        /// Unresolvable.
        /// </summary>
        public int Unresolvable { get; set; } = 0;

        /// <summary>
        /// Accurate plus half of partially accurate, over resolved.
        /// </summary>
        public double? AccuracyRate { get; set; } = null;

        /// <summary>
        /// Mean Brier score (lower is better).
        /// </summary>
        public double? MeanBrier { get; set; } = null;

        /// <summary>
        /// Mean days from horizon to resolution (negative when early).
        /// </summary>
        public double? MeanResolutionLatencyDays { get; set; } = null;

        /// <summary>
        /// Agent proposals the reader decided.
        /// </summary>
        public int ReviewsDecided { get; set; } = 0;

        /// <summary>
        /// Share of decided proposals the reader accepted.
        /// </summary>
        public double? AgentAgreement { get; set; } = null;

        /// <summary>
        /// Five confidence buckets.
        /// </summary>
        public List<CalibrationBucket> Calibration { get; set; } = new List<CalibrationBucket>();

        /// <summary>
        /// By topic (rate is accuracy, value is mean Brier).
        /// </summary>
        public List<BreakdownRow> ByTopic { get; set; } = new List<BreakdownRow>();
    }
}
