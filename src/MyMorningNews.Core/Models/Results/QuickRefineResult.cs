namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of refinement.quick: a brief for a one-time request.
    /// </summary>
    public class QuickRefineResult : IStructuredResult
    {
        /// <summary>
        /// Short title.
        /// </summary>
        [Documentation("A short title for the deliverable, 3 to 10 words.")]
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Research brief in Markdown.
        /// </summary>
        [Documentation("Markdown brief describing what to research and cover.")]
        public string Brief { get; set; } = String.Empty;

        /// <summary>
        /// Dimensions to cover.
        /// </summary>
        [Documentation("3 to 8 dimensions to cover.")]
        public List<ProposedDimension> Dimensions { get; set; } = new List<ProposedDimension>();

        /// <summary>
        /// Include keywords.
        /// </summary>
        [Documentation("Keywords to include.")]
        public List<string> IncludeKeywords { get; set; } = new List<string>();

        /// <summary>
        /// Exclude keywords.
        /// </summary>
        [Documentation("Keywords to exclude.")]
        public List<string> ExcludeKeywords { get; set; } = new List<string>();

        /// <summary>
        /// Seed search queries.
        /// </summary>
        [Documentation("5 to 10 seed search queries.")]
        public List<string> SeedQueries { get; set; } = new List<string>();

        /// <summary>
        /// Up to three clarifying questions.
        /// </summary>
        [Documentation("At most 3 clarifying questions whose answers would change the result; empty when the request is clear.")]
        public List<ProposedQuestion> Questions { get; set; } = new List<ProposedQuestion>();

        /// <summary>
        /// Why the request is not a research request; null otherwise.
        /// </summary>
        [Documentation("Set only when the request is not something to research and report on (for example a request to write code).")]
        public string? OutOfScopeReason { get; set; } = null;

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (!String.IsNullOrWhiteSpace(OutOfScopeReason)) return errors;
            if (String.IsNullOrWhiteSpace(Title)) errors.Add("title must not be empty.");
            if (String.IsNullOrWhiteSpace(Brief)) errors.Add("brief must not be empty.");
            if (SeedQueries == null || SeedQueries.Count == 0) errors.Add("seedQueries must contain at least one query.");
            if (Questions == null) Questions = new List<ProposedQuestion>();
            if (Questions.Count > 3) Questions = Questions.Take(3).ToList();
            return errors;
        }
    }
}
