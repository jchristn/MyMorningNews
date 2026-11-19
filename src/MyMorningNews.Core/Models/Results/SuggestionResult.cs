namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of research.suggest.
    /// </summary>
    public class SuggestionResult : IStructuredResult
    {
        /// <summary>
        /// Suggestions.
        /// </summary>
        [Documentation("Up to 5 suggestions.")]
        public List<ProposedSuggestion> Suggestions { get; set; } = new List<ProposedSuggestion>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (Suggestions == null) Suggestions = new List<ProposedSuggestion>();
            Suggestions = Suggestions.Where(x => x != null && !String.IsNullOrWhiteSpace(x.Title)).Take(5).ToList();
            return errors;
        }
    }
}
