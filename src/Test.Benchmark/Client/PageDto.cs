namespace MyMorningNews.Benchmark.Client
{
    using System.Collections.Generic;

    /// <summary>
    /// A page of results (the server's EnumerationResult).
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public class PageDto<T>
    {
        /// <summary>
        /// Items.
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Total matching records.
        /// </summary>
        public long TotalCount { get; set; } = 0;

        /// <summary>
        /// Server-side duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;
    }
}
