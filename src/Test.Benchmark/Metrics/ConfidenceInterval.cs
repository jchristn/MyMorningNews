namespace MyMorningNews.Benchmark.Metrics
{
    using System.Globalization;

    /// <summary>
    /// A mean with a 95% bootstrap confidence interval.
    /// </summary>
    public class ConfidenceInterval
    {
        /// <summary>
        /// Mean.
        /// </summary>
        public double Mean { get; set; } = 0;

        /// <summary>
        /// Lower bound.
        /// </summary>
        public double Low { get; set; } = 0;

        /// <summary>
        /// Upper bound.
        /// </summary>
        public double High { get; set; } = 0;

        /// <summary>
        /// Sample size.
        /// </summary>
        public int N { get; set; } = 0;

        /// <summary>
        /// Whether the group is too small for a headline number (fewer than 20 probes).
        /// </summary>
        public bool Indicative
        {
            get { return N < 20; }
        }

        /// <summary>
        /// Format as "0.812 [0.700, 0.900]".
        /// </summary>
        /// <returns>Text.</returns>
        public override string ToString()
        {
            return Mean.ToString("0.000", CultureInfo.InvariantCulture) + " [" + Low.ToString("0.000", CultureInfo.InvariantCulture) + ", " + High.ToString("0.000", CultureInfo.InvariantCulture) + "]";
        }
    }
}
