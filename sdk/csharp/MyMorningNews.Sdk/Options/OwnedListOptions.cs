namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// List options for resources owned by a user. Members always see their own items; tenant and global administrators see
    /// everyone's unless UserId or Mine narrows the list.
    /// </summary>
    public class OwnedListOptions : ListOptions
    {
        /// <summary>
        /// Administrators: only this user's items. Ignored for members. Null by default.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Administrators: only the caller's own items. Null by default.
        /// </summary>
        public bool? Mine { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OwnedListOptions()
        {
        }

        /// <summary>
        /// Add the owner filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "userId", UserId);
            QueryFormat.Add(query, "mine", Mine);
        }
    }
}
