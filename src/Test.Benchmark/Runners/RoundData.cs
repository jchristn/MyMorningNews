namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Benchmark.Client;

    /// <summary>
    /// What one round produced, as read back over the API (or built by a reference arm): the run, its search results,
    /// its findings mapped to items, and each finding's evidence.
    /// </summary>
    public class RoundData
    {
        #region Public-Members

        /// <summary>
        /// Round.
        /// </summary>
        public int Round { get; set; } = 1;

        /// <summary>
        /// Run, or null for a reference arm.
        /// </summary>
        public RunDto? Run { get; set; } = null;

        /// <summary>
        /// Search results.
        /// </summary>
        public List<SearchResultDto> Results { get; set; } = new List<SearchResultDto>();

        /// <summary>
        /// Every finding, including duplicates and those under the display threshold.
        /// </summary>
        public List<FindingDto> Findings { get; set; } = new List<FindingDto>();

        /// <summary>
        /// Item behind each finding (finding id to content item id).
        /// </summary>
        public Dictionary<string, string> ItemByFinding { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Evidence per finding.
        /// </summary>
        public Dictionary<string, List<EvidenceDto>> Evidence { get; set; } = new Dictionary<string, List<EvidenceDto>>(StringComparer.Ordinal);

        /// <summary>
        /// Item behind each search result (result id to item id as searched, which may be an alias).
        /// </summary>
        public Dictionary<string, string> ItemByResult { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Shown findings (not duplicates, above the display threshold) in rank order.
        /// </summary>
        /// <returns>Findings.</returns>
        public List<FindingDto> Shown()
        {
            return Findings.Where(f => f.Shown).OrderBy(f => f.Rank <= 0 ? Int32.MaxValue : f.Rank).ThenBy(f => f.Title, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Findings whose item is the given content item.
        /// </summary>
        /// <param name="itemId">Content item.</param>
        /// <returns>Findings.</returns>
        public List<FindingDto> FindingsOf(string itemId)
        {
            return Findings.Where(f => ItemByFinding.TryGetValue(f.Id, out string? i) && i == itemId).ToList();
        }

        /// <summary>
        /// Search results for an item as searched (alias or original).
        /// </summary>
        /// <param name="itemId">Item.</param>
        /// <returns>Results.</returns>
        public List<SearchResultDto> ResultsOf(string itemId)
        {
            return Results.Where(r => ItemByResult.TryGetValue(r.Id, out string? i) && i == itemId).ToList();
        }

        #endregion
    }
}
