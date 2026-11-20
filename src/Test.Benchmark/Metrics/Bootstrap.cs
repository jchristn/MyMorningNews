namespace MyMorningNews.Benchmark.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Percentile bootstrap with a fixed seed: a 95% confidence interval for a mean, and a paired test for the
    /// difference of two means. Thread safe (each call has its own generator).
    /// </summary>
    public static class Bootstrap
    {
        #region Public-Members

        /// <summary>
        /// Default resamples.
        /// </summary>
        public const int DefaultResamples = 2000;

        /// <summary>
        /// Default seed.
        /// </summary>
        public const int DefaultSeed = 20261120;

        #endregion

        #region Public-Methods

        /// <summary>
        /// 95% confidence interval for the mean.
        /// </summary>
        /// <param name="values">Values.</param>
        /// <param name="resamples">Resamples, 100 to 100000.</param>
        /// <param name="seed">Seed.</param>
        /// <returns>Interval; for an empty list, all zeros with N = 0.</returns>
        public static ConfidenceInterval Mean(IReadOnlyList<double> values, int resamples = DefaultResamples, int seed = DefaultSeed)
        {
            ArgumentNullException.ThrowIfNull(values);
            ConfidenceInterval ci = new ConfidenceInterval { N = values.Count };
            if (values.Count == 0) return ci;
            ci.Mean = values.Average();
            int b = Math.Clamp(resamples, 100, 100000);
            Random rng = new Random(seed);
            double[] means = new double[b];
            for (int i = 0; i < b; i++)
            {
                double sum = 0;
                for (int j = 0; j < values.Count; j++) sum += values[rng.Next(values.Count)];
                means[i] = sum / values.Count;
            }

            Array.Sort(means);
            ci.Low = means[(int)Math.Floor(0.025 * (b - 1))];
            ci.High = means[(int)Math.Ceiling(0.975 * (b - 1))];
            return ci;
        }

        /// <summary>
        /// Paired bootstrap of candidate minus baseline: the mean difference, its 95% interval, and a two-sided p-value
        /// (twice the smaller tail share of resampled differences at or beyond zero).
        /// </summary>
        /// <param name="baseline">Baseline values.</param>
        /// <param name="candidate">Candidate values, paired by index.</param>
        /// <param name="resamples">Resamples, 100 to 100000.</param>
        /// <param name="seed">Seed.</param>
        /// <returns>Result.</returns>
        /// <exception cref="ArgumentException">The lists differ in length.</exception>
        public static PairedResult Paired(IReadOnlyList<double> baseline, IReadOnlyList<double> candidate, int resamples = DefaultResamples, int seed = DefaultSeed)
        {
            ArgumentNullException.ThrowIfNull(baseline);
            ArgumentNullException.ThrowIfNull(candidate);
            if (baseline.Count != candidate.Count) throw new ArgumentException("Paired lists must have the same length.");
            PairedResult result = new PairedResult { N = baseline.Count };
            if (baseline.Count == 0) { result.P = 1; return result; }
            double[] diffs = baseline.Select((b, i) => candidate[i] - b).ToArray();
            result.Delta = diffs.Average();
            if (diffs.All(d => Math.Abs(d) < 1e-12)) { result.P = 1; return result; }
            int n = Math.Clamp(resamples, 100, 100000);
            Random rng = new Random(seed);
            double[] means = new double[n];
            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                for (int j = 0; j < diffs.Length; j++) sum += diffs[rng.Next(diffs.Length)];
                means[i] = sum / diffs.Length;
            }

            Array.Sort(means);
            result.Low = means[(int)Math.Floor(0.025 * (n - 1))];
            result.High = means[(int)Math.Ceiling(0.975 * (n - 1))];
            double below = means.Count(m => m <= 0) / (double)n;
            double above = means.Count(m => m >= 0) / (double)n;
            result.P = Math.Min(1, 2 * Math.Min(below, above));
            return result;
        }

        #endregion
    }
}
