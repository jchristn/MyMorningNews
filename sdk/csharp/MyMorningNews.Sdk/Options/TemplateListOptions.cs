namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// Filters for listing briefing templates.
    /// </summary>
    public class TemplateListOptions : ListOptions
    {
        /// <summary>
        /// all (default), shared, or mine. Null by default.
        /// </summary>
        public string? Scope { get; set; } = null;

        /// <summary>
        /// Administrators: include this user's templates. Null by default.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TemplateListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "scope", Scope);
            QueryFormat.Add(query, "userId", UserId);
        }
    }
}
