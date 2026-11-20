namespace MyMorningNews.Benchmark.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Ranking metrics computed the standard way: nDCG@k with linear gain (the graded relevance itself, as pytrec_eval
    /// and BEIR compute it) and a log2(rank + 1) discount, and precision@k. Thread safe.
    /// </summary>
    public static class RankingMetrics
    {
        #region Public-Methods

        /// <summary>
        /// nDCG@k with linear gain.
        /// </summary>
        /// <param name="rankedGains">Gains of the returned list, best first.</param>
        /// <param name="allGains">Gains of every judged item (for the ideal ranking).</param>
        /// <param name="k">Cutoff, 1 or more.</param>
        /// <returns>nDCG, 0 to 1. When no judged item has positive gain, 1 for an empty list and 0 for a non-empty one
        /// (the correct answer was "nothing").</returns>
        public static double Ndcg(IReadOnlyList<double> rankedGains, IEnumerable<double> allGains, int k)
        {
            ArgumentNullException.ThrowIfNull(rankedGains);
            ArgumentNullException.ThrowIfNull(allGains);
            int cut = Math.Max(1, k);
            double dcg = Dcg(rankedGains.Take(cut));
            double idcg = Dcg(allGains.Where(g => g > 0).OrderByDescending(g => g).Take(cut));
            if (idcg <= 0) return rankedGains.Count == 0 ? 1.0 : 0.0;
            return dcg / idcg;
        }

        /// <summary>
        /// Precision@k: the share of the first k returned items that are relevant. When fewer than k items were returned,
        /// the denominator is the number returned (1 when none were returned and none were expected).
        /// </summary>
        /// <param name="rankedRelevant">Whether each returned item is relevant, best first.</param>
        /// <param name="k">Cutoff.</param>
        /// <returns>Precision, 0 to 1.</returns>
        public static double Precision(IReadOnlyList<bool> rankedRelevant, int k)
        {
            ArgumentNullException.ThrowIfNull(rankedRelevant);
            List<bool> top = rankedRelevant.Take(Math.Max(1, k)).ToList();
            if (top.Count == 0) return 0;
            return (double)top.Count(x => x) / top.Count;
        }

        #endregion

        #region Private-Methods

        private static double Dcg(IEnumerable<double> gains)
        {
            double dcg = 0;
            int i = 1;
            foreach (double g in gains)
            {
                dcg += g / Math.Log2(i + 1);
                i++;
            }

            return dcg;
        }

        #endregion
    }
}
