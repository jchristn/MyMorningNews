namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>Browse the shared catalog with paging and optional category/language filters.</summary>
    public class CatalogListOptions : ListOptions
    {
        /// <summary>Category, or null for all.</summary>
        public string? Category { get; set; } = null;
        /// <summary>Language code, or null for all.</summary>
        public string? Language { get; set; } = null;
        /// <inheritdoc />
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "category", Category);
            QueryFormat.Add(query, "language", Language);
        }
    }
}
