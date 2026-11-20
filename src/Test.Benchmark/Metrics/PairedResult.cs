namespace MyMorningNews.Benchmark.Metrics
{
    /// <summary>
    /// A paired bootstrap comparison (candidate minus baseline).
    /// </summary>
    public class PairedResult
    {
        /// <summary>
        /// Mean difference.
        /// </summary>
        public double Delta { get; set; } = 0;

        /// <summary>
        /// Lower bound of the 95% interval.
        /// </summary>
        public double Low { get; set; } = 0;

        /// <summary>
        /// Upper bound of the 95% interval.
        /// </summary>
        public double High { get; set; } = 0;

        /// <summary>
        /// Two-sided p-value.
        /// </summary>
        public double P { get; set; } = 1;

        /// <summary>
        /// Pairs.
        /// </summary>
        public int N { get; set; } = 0;
    }
}
