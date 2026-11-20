namespace MyMorningNews.Benchmark.Datasets
{
    using System;

    /// <summary>
    /// A topic constraint, such as a blocked domain.
    /// </summary>
    public class BenchmarkConstraint
    {
        /// <summary>
        /// Kind, as the product names it (BlockedDomain, PreferredDomain, IncludeKeyword, ExcludeKeyword, and so on).
        /// </summary>
        public string Kind { get; set; } = String.Empty;

        /// <summary>
        /// Value.
        /// </summary>
        public string Value { get; set; } = String.Empty;
    }
}
