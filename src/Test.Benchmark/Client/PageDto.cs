namespace MyMorningNews.Benchmark.Client
{
    using System.Collections.Generic;

    /// <summary>
    /// A page of results (the server's EnumerationResult).
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public class PageDto<T>
    {
        /// <summary>One-based page number.</summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>Maximum objects per page.</summary>
        public int PageSize { get; set; } = 10;

        /// <summary>Total matching pages.</summary>
        public int TotalPages { get; set; } = 0;

        /// <summary>Whether enumeration succeeded.</summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Objects.
        /// </summary>
        public List<T> Objects { get; set; } = new List<T>();

        /// <summary>
        /// Total matching records.
        /// </summary>
        public long TotalRecords { get; set; } = 0;

        /// <summary>
        /// Server-side duration in milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0;
    }
}
