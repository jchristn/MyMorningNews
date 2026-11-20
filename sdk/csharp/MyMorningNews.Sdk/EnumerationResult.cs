namespace MyMorningNews.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// One page of a list endpoint.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public class EnumerationResult<T>
    {
        /// <summary>
        /// Items on this page. Never null.
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// One-based page number. Default 1.
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Page size. Default 25.
        /// </summary>
        public int PageSize { get; set; } = 25;

        /// <summary>
        /// Items matching the filter across all pages.
        /// </summary>
        public long TotalCount { get; set; } = 0;

        /// <summary>
        /// Pages available at this page size.
        /// </summary>
        public int TotalPages { get; set; } = 0;

        /// <summary>
        /// Server time spent on the query, in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EnumerationResult()
        {
        }
    }
}
