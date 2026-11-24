namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Common paging, sorting, and date-range filter for list endpoints.
    /// </summary>
    public class EnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Tenant scope. Set from the request context, never trusted from the caller for ordinary operations.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// For entities with optional tenants (built-in roles and permissions): also include global rows with a null tenant.
        /// </summary>
        public bool IncludeGlobal { get; set; } = false;

        /// <summary>
        /// For entities with optional tenants: ignore tenant scoping entirely. Control-plane use by global administrators only.
        /// </summary>
        public bool AllTenants { get; set; } = false;

        /// <summary>
        /// One-based page number. Default 1. Minimum 1.
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
        /// Page size. Default 10. Minimum 1, maximum 1000.
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
        /// Sort field (camelCase property name). Unknown values fall back to the default sort.
        /// </summary>
        public string? SortBy { get; set; } = null;

        /// <summary>
        /// Sort direction. Default Descending.
        /// </summary>
        public SortDirectionEnum SortDirection { get; set; } = SortDirectionEnum.Descending;

        /// <summary>
        /// Free-text search over the entity's searchable columns.
        /// </summary>
        public string? Search { get; set; } = null;

        /// <summary>
        /// Inclusive lower bound on creation time (UTC).
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Exclusive upper bound on creation time (UTC).
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>Creation ordering, newest first by default. Sets the shared sorting fields.</summary>
        public EnumerationOrderEnum Order
        {
            get { return SortDirection == SortDirectionEnum.Ascending ? EnumerationOrderEnum.CreatedAscending : EnumerationOrderEnum.CreatedDescending; }
            set { SortBy = "createdUtc"; SortDirection = value == EnumerationOrderEnum.CreatedAscending ? SortDirectionEnum.Ascending : SortDirectionEnum.Descending; }
        }

        /// <summary>Exclusive creation lower bound; null leaves it unrestricted.</summary>
        public DateTime? CreatedAfter { get; set; } = null;

        /// <summary>Exclusive creation upper bound; null leaves it unrestricted.</summary>
        public DateTime? CreatedBefore { get; set; } = null;

        /// <summary>Overflow-safe database offset.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public long Offset { get { return (long)(PageNumber - 1) * PageSize; } }

        #endregion

        #region Private-Members

        private int _PageNumber = 1;
        private int _PageSize = 10;

        #endregion
    }
}
