namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Stated confidence against observed accuracy.
    /// </summary>
    public class CalibrationBucket
    {
        /// <summary>
        /// Lower confidence bound.
        /// </summary>
        public double Lower { get; set; } = 0;

        /// <summary>
        /// Upper confidence bound.
        /// </summary>
        public double Upper { get; set; } = 0;

        /// <summary>
        /// Resolved predictions.
        /// </summary>
        public int Count { get; set; } = 0;

        /// <summary>
        /// Mean stated confidence.
        /// </summary>
        public double? MeanConfidence { get; set; } = null;

        /// <summary>
        /// Observed accuracy (accurate plus half of partially accurate).
        /// </summary>
        public double? Observed { get; set; } = null;
    }
}
