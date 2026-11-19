namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of refinement.sources.
    /// </summary>
    public class SourceSuggestionResult : IStructuredResult
    {
        /// <summary>
        /// RSS or Atom feeds.
        /// </summary>
        [Documentation("Up to 12 RSS or Atom feed URLs.")]
        public List<SuggestedSource> Feeds { get; set; } = new List<SuggestedSource>();

        /// <summary>
        /// Seed pages.
        /// </summary>
        [Documentation("Up to 12 seed page URLs.")]
        public List<SuggestedSource> SeedUrls { get; set; } = new List<SuggestedSource>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (Feeds == null) Feeds = new List<SuggestedSource>();
            if (SeedUrls == null) SeedUrls = new List<SuggestedSource>();
            Feeds = Feeds.Where(s => s != null && Uri.TryCreate(s.Url, UriKind.Absolute, out Uri? u) && (u.Scheme == "https" || u.Scheme == "http")).Take(12).ToList();
            SeedUrls = SeedUrls.Where(s => s != null && Uri.TryCreate(s.Url, UriKind.Absolute, out Uri? u) && (u.Scheme == "https" || u.Scheme == "http")).Take(12).ToList();
            return errors;
        }
    }
}
