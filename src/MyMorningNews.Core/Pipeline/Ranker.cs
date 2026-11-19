namespace MyMorningNews.Core.Pipeline
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Composite relevance scoring (plan section 17). Every component is 0 to 100, and the composite is the weighted
    /// average of the components, so weights do not have to sum to one.
    /// Thread safety: thread safe.
    /// </summary>
    public static class Ranker
    {
        /// <summary>
        /// Recency: 100 for something published now, halving every half-life. Unknown dates score a neutral 50.
        /// </summary>
        /// <param name="publishedUtc">Publication time.</param>
        /// <param name="nowUtc">Current time.</param>
        /// <param name="halfLifeHours">Half-life in hours. Minimum 1.</param>
        /// <returns>0 to 100.</returns>
        public static double Recency(DateTime? publishedUtc, DateTime nowUtc, double halfLifeHours)
        {
            if (!publishedUtc.HasValue) return 50;
            double hours = Math.Max(0, (nowUtc - publishedUtc.Value).TotalHours);
            return Math.Round(100 * Math.Pow(0.5, hours / Math.Max(1, halfLifeHours)), 2);
        }

        /// <summary>
        /// Novelty: 100 × (1 - highest similarity to recent findings), blended toward a neutral 50 as the topic's
        /// novelty preference drops (0 ignores novelty, 100 counts it fully).
        /// </summary>
        /// <param name="maxSimilarity">Highest cosine similarity to recent findings, or null when unknown.</param>
        /// <param name="noveltyPreference">Topic novelty preference, 0 to 100.</param>
        /// <returns>0 to 100.</returns>
        public static double Novelty(double? maxSimilarity, int noveltyPreference)
        {
            double raw = maxSimilarity.HasValue ? 100 * (1 - Math.Clamp(maxSimilarity.Value, 0, 1)) : 100;
            double p = Math.Clamp(noveltyPreference, 0, 100) / 100.0;
            return Math.Round(raw * p + 50 * (1 - p), 2);
        }

        /// <summary>
        /// Source reputation: the reader's average rating for the domain (0 to 100) pulled toward a prior of 50 with a
        /// prior weight of 5, plus 20 for preferred domains. Blocked domains score 0.
        /// </summary>
        /// <param name="ratingAverage">Average rating for the domain (0 to 100), or null when unrated.</param>
        /// <param name="ratingCount">Ratings for the domain.</param>
        /// <param name="preferred">Domain is preferred for the topic.</param>
        /// <param name="blocked">Domain is blocked.</param>
        /// <returns>0 to 100.</returns>
        public static double Source(double? ratingAverage, int ratingCount, bool preferred, bool blocked)
        {
            if (blocked) return 0;
            double bayes = (50 * 5 + (ratingAverage ?? 50) * Math.Max(0, ratingCount)) / (5 + Math.Max(0, ratingCount));
            return Math.Round(Math.Clamp(bayes + (preferred ? 20 : 0), 0, 100), 2);
        }

        /// <summary>
        /// Weighted composite.
        /// </summary>
        /// <param name="llm">Model judgment.</param>
        /// <param name="feedback">Feedback similarity.</param>
        /// <param name="novelty">Novelty.</param>
        /// <param name="source">Source reputation.</param>
        /// <param name="recency">Recency.</param>
        /// <param name="wLlm">Weight of the model judgment.</param>
        /// <param name="wFeedback">Weight of feedback.</param>
        /// <param name="wNovelty">Weight of novelty.</param>
        /// <param name="wSource">Weight of source.</param>
        /// <param name="wRecency">Weight of recency.</param>
        /// <returns>0 to 100.</returns>
        public static double Composite(double llm, double feedback, double novelty, double source, double recency, double wLlm, double wFeedback, double wNovelty, double wSource, double wRecency)
        {
            double total = Math.Max(0, wLlm) + Math.Max(0, wFeedback) + Math.Max(0, wNovelty) + Math.Max(0, wSource) + Math.Max(0, wRecency);
            if (total <= 0) return Math.Round(Math.Clamp(llm, 0, 100), 2);
            double sum = Math.Max(0, wLlm) * llm + Math.Max(0, wFeedback) * feedback + Math.Max(0, wNovelty) * novelty + Math.Max(0, wSource) * source + Math.Max(0, wRecency) * recency;
            return Math.Round(Math.Clamp(sum / total, 0, 100), 2);
        }

        /// <summary>
        /// Tier for a composite score.
        /// </summary>
        /// <param name="score">Composite score.</param>
        /// <param name="displayThreshold">Topic display threshold.</param>
        /// <returns>Tier.</returns>
        public static FindingTierEnum Tier(double score, int displayThreshold)
        {
            if (score < displayThreshold) return FindingTierEnum.BelowThreshold;
            if (score >= 80) return FindingTierEnum.MustRead;
            if (score >= 60) return FindingTierEnum.WorthALook;
            return FindingTierEnum.Fyi;
        }
    }
}
