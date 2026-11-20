namespace MyMorningNews.Sdk.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a learning pass for a topic (POST /v1.0/api/topics/{id}/learn).
    /// </summary>
    public class LearnResult
    {
        /// <summary>
        /// Preferences proposed by this pass.
        /// </summary>
        public int Proposed { get; set; } = 0;

        /// <summary>
        /// The topic's learned preferences after the pass. Never null.
        /// </summary>
        public List<LearnedPreference> Preferences { get; set; } = new List<LearnedPreference>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LearnResult()
        {
        }
    }
}
