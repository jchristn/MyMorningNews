namespace MyMorningNews.Core.Briefings
{
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Structured changes computed for one composition, before they are saved.
    /// </summary>
    public class ComparisonResult
    {
        /// <summary>
        /// Changes in display order, not yet saved (BriefingId is set when saving).
        /// </summary>
        public List<BriefingDelta> Deltas { get; set; } = new List<BriefingDelta>();

        /// <summary>
        /// One-line summary for the briefing text, or null when nothing was compared.
        /// </summary>
        public string? Sentence { get; set; } = null;
    }
}
