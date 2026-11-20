namespace MyMorningNews.Benchmark.Metrics
{
    using System;

    /// <summary>
    /// Time attributed to one stage (or label value) from the server's histograms.
    /// </summary>
    public class StageTime
    {
        /// <summary>
        /// Stage or label value.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Observations.
        /// </summary>
        public long Count { get; set; } = 0;

        /// <summary>
        /// Total seconds.
        /// </summary>
        public double TotalSeconds { get; set; } = 0;

        /// <summary>
        /// Mean milliseconds per observation.
        /// </summary>
        public double MeanMs { get; set; } = 0;
    }
}
