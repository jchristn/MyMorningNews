namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Requests;

    /// <summary>
    /// One page of results.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public class EnumerationResult<T>
    {
        #region Public-Members

        /// <summary>Whether enumeration succeeded.</summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Objects on this page.
        /// </summary>
        public List<T> Objects { get; set; } = new List<T>();

        /// <summary>
        /// One-based page number.
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// Total matching records.
        /// </summary>
        public long TotalRecords { get; set; } = 0;

        /// <summary>
        /// Total pages.
        /// </summary>
        public int TotalPages { get; set; } = 0;

        /// <summary>
        /// Query duration in milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a result page.
        /// </summary>
        /// <param name="items">Objects.</param>
        /// <param name="filter">Filter used.</param>
        /// <param name="totalCount">Total matching records.</param>
        /// <param name="durationMs">Duration in milliseconds.</param>
        /// <returns>Result page.</returns>
        /// <exception cref="ArgumentNullException">items or filter is null.</exception>
        public static EnumerationResult<T> Create(List<T> items, EnumerationQuery filter, long totalCount, double durationMs)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(filter);
            return new EnumerationResult<T>
            {
                Objects = items,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalRecords = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize),
                TotalMs = Math.Round(durationMs, 2)
            };
        }

        /// <summary>
        /// Map items to another type while keeping paging metadata.
        /// </summary>
        /// <typeparam name="TOut">Output item type.</typeparam>
        /// <param name="map">Mapping function.</param>
        /// <returns>Mapped result.</returns>
        /// <exception cref="ArgumentNullException">map is null.</exception>
        public EnumerationResult<TOut> Map<TOut>(Func<T, TOut> map)
        {
            ArgumentNullException.ThrowIfNull(map);
            return new EnumerationResult<TOut>
            {
                Objects = Objects.ConvertAll(new Converter<T, TOut>(map)),
                PageNumber = PageNumber,
                PageSize = PageSize,
                TotalRecords = TotalRecords,
                TotalPages = TotalPages,
                TotalMs = TotalMs
            };
        }

        #endregion
    }
}
