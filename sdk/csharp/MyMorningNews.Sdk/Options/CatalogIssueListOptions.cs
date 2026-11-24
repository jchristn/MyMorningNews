namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>Aggregate shared issues, optionally limited to issues with ready audio.</summary>
    public class CatalogIssueListOptions : ListOptions
    {
        /// <summary>True limits results to audio-ready issues. Default false.</summary>
        public bool AudioOnly { get; set; } = false;
        /// <inheritdoc />
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "audioOnly", AudioOnly);
        }
    }
}
