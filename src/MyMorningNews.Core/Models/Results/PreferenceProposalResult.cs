namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of feedback.distill-preferences.
    /// </summary>
    public class PreferenceProposalResult : IStructuredResult
    {
        /// <summary>
        /// Proposals.
        /// </summary>
        [Documentation("Up to 5 proposed preferences.")]
        public List<ProposedPreference> Preferences { get; set; } = new List<ProposedPreference>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            if (Preferences == null) Preferences = new List<ProposedPreference>();
            foreach (ProposedPreference p in Preferences.Where(p => p != null))
            {
                p.Confidence = Math.Clamp(p.Confidence, 0, 1);
                p.FeedbackIds = (p.FeedbackIds ?? new List<string>()).Where(x => !String.IsNullOrWhiteSpace(x)).Distinct().ToList();
            }

            Preferences = Preferences.Where(p => p != null && !String.IsNullOrWhiteSpace(p.Statement)).Take(5).ToList();
            return new List<string>();
        }
    }
}
