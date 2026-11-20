namespace MyMorningNews.Sdk.Options
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Paging, sorting, search, and date range for list endpoints. Resource-specific subclasses add typed filters.
    /// </summary>
    public class ListOptions
    {
        /// <summary>
        /// One-based page number. Default 1. Minimum 1; lower values are raised to 1.
        /// </summary>
        public int PageNumber
        {
            get
            {
                return _PageNumber;
            }
            set
            {
                _PageNumber = Math.Max(1, value);
            }
        }

        /// <summary>
        /// Page size. Default 25. Minimum 1, maximum 1000; values outside the range are clamped.
        /// </summary>
        public int PageSize
        {
            get
            {
                return _PageSize;
            }
            set
            {
                _PageSize = Math.Clamp(value, 1, 1000);
            }
        }

        /// <summary>
        /// Property to sort by (for example createdUtc), or null for the route's default.
        /// </summary>
        public string? SortBy { get; set; } = null;

        /// <summary>
        /// Sort direction, or null for the route's default (usually Descending).
        /// </summary>
        public SortDirectionEnum? SortDirection { get; set; } = null;

        /// <summary>
        /// Free-text search, or null.
        /// </summary>
        public string? Search { get; set; } = null;

        /// <summary>
        /// Only items created at or after this time, or null.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Only items created before this time, or null.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        private int _PageNumber = 1;
        private int _PageSize = 25;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ListOptions()
        {
        }

        /// <summary>
        /// Build the query string values.
        /// </summary>
        /// <returns>Query values.</returns>
        public Dictionary<string, string> ToQuery()
        {
            Dictionary<string, string> query = new Dictionary<string, string>(StringComparer.Ordinal);
            QueryFormat.Add(query, "pageNumber", PageNumber);
            QueryFormat.Add(query, "pageSize", PageSize);
            QueryFormat.Add(query, "sortBy", SortBy);
            QueryFormat.Add(query, "sortDirection", SortDirection);
            QueryFormat.Add(query, "search", Search);
            QueryFormat.Add(query, "fromUtc", FromUtc);
            QueryFormat.Add(query, "toUtc", ToUtc);
            AddFilters(query);
            return query;
        }

        /// <summary>
        /// Add resource-specific filters. The base class adds none.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected virtual void AddFilters(Dictionary<string, string> query)
        {
        }
    }
}
