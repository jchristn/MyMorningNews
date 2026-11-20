namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// Filters for listing users.
    /// </summary>
    public class UserListOptions : ListOptions
    {
        /// <summary>
        /// Only active (true) or inactive (false) users. Null by default.
        /// </summary>
        public bool? Active { get; set; } = null;

        /// <summary>
        /// Only global administrators (true) or not (false). Null by default.
        /// </summary>
        public bool? IsAdmin { get; set; } = null;

        /// <summary>
        /// Only tenant administrators (true) or not (false). Null by default.
        /// </summary>
        public bool? IsTenantAdmin { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public UserListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "active", Active);
            QueryFormat.Add(query, "isAdmin", IsAdmin);
            QueryFormat.Add(query, "isTenantAdmin", IsTenantAdmin);
        }
    }
}
