namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// Browse the shared catalog with paging and optional category, language, and subscription filters. SortBy accepts
    /// title, sortOrder, createdUtc, or lastUpdateUtc; Search matches title and description.
    /// </summary>
    public class CatalogListOptions : ListOptions
    {
        /// <summary>Category, or null for all.</summary>
        public string? Category { get; set; } = null;
        /// <summary>Language code, or null for all.</summary>
        public string? Language { get; set; } = null;
        /// <summary>True for only the caller's active subscriptions, false for only the rest, or null for all. Default null.</summary>
        public bool? Subscribed { get; set; } = null;
        /// <inheritdoc />
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "category", Category);
            QueryFormat.Add(query, "language", Language);
            QueryFormat.Add(query, "subscribed", Subscribed);
        }
    }
}
