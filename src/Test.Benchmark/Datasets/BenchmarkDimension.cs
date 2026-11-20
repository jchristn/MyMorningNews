namespace MyMorningNews.Benchmark.Datasets
{
    using System;

    /// <summary>
    /// A topic dimension.
    /// </summary>
    public class BenchmarkDimension
    {
        /// <summary>
        /// Name. Required.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Weight: Exclude, Low, Normal, or High. Default Normal.
        /// </summary>
        public string Weight { get; set; } = "Normal";

        /// <summary>
        /// Description, or null.
        /// </summary>
        public string? Description { get; set; } = null;
    }
}
