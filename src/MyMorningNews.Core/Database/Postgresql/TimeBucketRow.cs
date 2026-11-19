namespace MyMorningNews.Core.Database.Postgresql
{
    /// <summary>
    /// Aggregated row for one time bucket, used by summary queries.
    /// </summary>
    public class TimeBucketRow
    {
        /// <summary>
        /// Zero-based bucket index from the start of the range.
        /// </summary>
        public long Index { get; set; } = 0;

        /// <summary>
        /// Successful count.
        /// </summary>
        public long Success { get; set; } = 0;

        /// <summary>
        /// Failed count.
        /// </summary>
        public long Failure { get; set; } = 0;

        /// <summary>
        /// Average value (for example duration in milliseconds).
        /// </summary>
        public double Average { get; set; } = 0;
    }
}
