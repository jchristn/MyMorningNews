namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// Filters for listing roles.
    /// </summary>
    public class RoleListOptions : ListOptions
    {
        /// <summary>
        /// Only built-in (true) or tenant (false) roles. Null by default.
        /// </summary>
        public bool? IsBuiltIn { get; set; } = null;

        /// <summary>
        /// Only active (true) or inactive (false) roles. Null by default.
        /// </summary>
        public bool? Active { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RoleListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "isBuiltIn", IsBuiltIn);
            QueryFormat.Add(query, "active", Active);
        }
    }
}
