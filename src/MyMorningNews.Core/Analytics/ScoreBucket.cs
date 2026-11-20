namespace MyMorningNews.Core.Analytics
{
    /// <summary>
    /// Findings whose relevance score falls in one bucket. The lower bound is inclusive and the upper bound exclusive,
    /// except the last bucket, which includes 100.
    /// </summary>
    public class ScoreBucket
    {
        /// <summary>
        /// Lower bound, 0 to 90.
        /// </summary>
        public int Lower { get; set; } = 0;

        /// <summary>
        /// Upper bound, 10 to 100.
        /// </summary>
        public int Upper { get; set; } = 10;

        /// <summary>
        /// Findings in the bucket.
        /// </summary>
        public int Count { get; set; } = 0;
    }
}
