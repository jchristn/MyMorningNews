namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of research.agentic-search: the pages to read, chosen by the model after searching and reading.
    /// </summary>
    public class AgenticSearchResult : IStructuredResult
    {
        /// <summary>
        /// Pages to read, most promising first.
        /// </summary>
        [Documentation("Pages worth reading, most promising first.")]
        public List<AgenticPick> Picks { get; set; } = new List<AgenticPick>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            Picks = (Picks ?? new List<AgenticPick>())
                .Where(p => p != null && Uri.TryCreate(p.Url?.Trim(), UriKind.Absolute, out Uri? u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
                .GroupBy(p => p.Url.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            foreach (AgenticPick p in Picks) p.Url = p.Url.Trim();
            if (Picks.Count == 0) errors.Add("picks must contain at least one http or https page URL you found.");
            return errors;
        }
    }
}
