namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A proposed suggestion.
    /// </summary>
    public class ProposedSuggestion
    {
        /// <summary>
        /// Title.
        /// </summary>
        [Documentation("Short title.")]
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Description.
        /// </summary>
        [Documentation("What to do.")]
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Rationale.
        /// </summary>
        [Documentation("Why.")]
        public string Rationale { get; set; } = String.Empty;

        /// <summary>
        /// Priority.
        /// </summary>
        [Documentation("High, Medium, or Low.")]
        public SuggestionPriorityEnum Priority { get; set; } = SuggestionPriorityEnum.Medium;

        /// <summary>
        /// Effort.
        /// </summary>
        [Documentation("Minutes, Hours, or Days.")]
        public SuggestionEffortEnum Effort { get; set; } = SuggestionEffortEnum.Hours;

        /// <summary>
        /// Finding titles.
        /// </summary>
        [Documentation("Titles of findings it builds on.")]
        public List<string> Findings { get; set; } = new List<string>();
    }
}
