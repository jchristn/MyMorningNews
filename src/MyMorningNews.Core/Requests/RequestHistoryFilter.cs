namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Filter for request history listing, deletion, and summaries.
    /// </summary>
    public class RequestHistoryFilter : EnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// User identifier filter (administrators only).
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// HTTP method filter.
        /// </summary>
        public string? Method { get; set; } = null;

        /// <summary>
        /// Exact status code filter.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Minimum status code, inclusive (for example 400 for all failures). Null for no lower bound.
        /// </summary>
        public int? MinStatusCode { get; set; } = null;

        /// <summary>
        /// Maximum status code, inclusive (for example 499 for client errors). Null for no upper bound.
        /// </summary>
        public int? MaxStatusCode { get; set; } = null;

        /// <summary>
        /// Path substring filter.
        /// </summary>
        public string? PathContains { get; set; } = null;

        /// <summary>
        /// Summary bucket width in minutes. Default 15. Minimum 1, maximum 10080.
        /// </summary>
        public int BucketMinutes
        {
            get
            {
                return _BucketMinutes;
            }
            set
            {
                _BucketMinutes = Math.Clamp(value, 1, 10080);
            }
        }

        #endregion

        #region Private-Members

        private int _BucketMinutes = 15;

        #endregion
    }
}
