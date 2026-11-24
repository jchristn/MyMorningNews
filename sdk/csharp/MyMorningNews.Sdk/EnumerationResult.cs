namespace MyMorningNews.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// One page of a list endpoint.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public class EnumerationResult<T>
    {
        /// <summary>Whether enumeration succeeded.</summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Objects on this page. Never null.
        /// </summary>
        public List<T> Objects { get; set; } = new List<T>();

        /// <summary>
        /// One-based page number. Default 1.
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Page size. Default 10.
        /// </summary>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// Objects matching the filter across all pages.
        /// </summary>
        public long TotalRecords { get; set; } = 0;

        /// <summary>
        /// Pages available at this page size.
        /// </summary>
        public int TotalPages { get; set; } = 0;

        /// <summary>
        /// Server time spent on the query, in milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EnumerationResult()
        {
        }
    }
}
