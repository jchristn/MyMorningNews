namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Suggested ranking weights for a topic from how its rated findings scored (plan section 17).
    /// </summary>
    public class RankingRecommendation
    {
        /// <summary>
        /// Rated findings used.
        /// </summary>
        public int RatedFindings { get; set; } = 0;

        /// <summary>
        /// Whether there were enough ratings (20) to recommend.
        /// </summary>
        public bool Enough { get; set; } = false;

        /// <summary>
        /// Current weights by component.
        /// </summary>
        public Dictionary<string, double> Current { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Recommended weights by component.
        /// </summary>
        public Dictionary<string, double> Recommended { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Mean component score of positive minus negative ratings.
        /// </summary>
        public Dictionary<string, double> Separation { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Explanation.
        /// </summary>
        public string Note { get; set; } = String.Empty;
    }
}
