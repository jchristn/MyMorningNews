namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// Filters for listing credentials.
    /// </summary>
    public class CredentialListOptions : ListOptions
    {
        /// <summary>
        /// Only credentials of this user. Null by default.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Only active (true) or inactive (false) credentials. Null by default.
        /// </summary>
        public bool? Active { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CredentialListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "userId", UserId);
            QueryFormat.Add(query, "active", Active);
        }
    }
}
