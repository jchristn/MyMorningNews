namespace MyMorningNews.Benchmark.Runners
{
    using System;

    /// <summary>
    /// A paid run reached its spend cap; no further runs are started and the report is marked incomplete.
    /// </summary>
    public class SpendCapReachedException : Exception
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        public SpendCapReachedException(string message) : base(message)
        {
        }
    }
}
