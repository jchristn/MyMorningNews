namespace MyMorningNews.Benchmark.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// One scrape of the server's Prometheus exporter: every sample keyed by its full series name with labels. The
    /// harness diffs two snapshots to attribute time to stages from the server's own duration histograms.
    /// </summary>
    public class PrometheusSnapshot
    {
        #region Public-Members

        /// <summary>
        /// Samples by series (name and labels exactly as exported).
        /// </summary>
        public Dictionary<string, double> Samples { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// Whether the scrape succeeded.
        /// </summary>
        public bool Available { get; set; } = false;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Scrape a metrics URL.
        /// </summary>
        /// <param name="http">HTTP client.</param>
        /// <param name="url">Metrics URL, or "none" to skip.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Snapshot; Available is false when the scrape failed or was skipped.</returns>
        public static async Task<PrometheusSnapshot> ScrapeAsync(HttpClient http, string url, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(http);
            ArgumentNullException.ThrowIfNull(url);
            PrometheusSnapshot snap = new PrometheusSnapshot();
            if (String.Equals(url, "none", StringComparison.OrdinalIgnoreCase)) return snap;
            try
            {
                string text = await http.GetStringAsync(url, token).ConfigureAwait(false);
                foreach (string raw in text.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#')) continue;
                    int space = line.LastIndexOf(' ');
                    if (space <= 0) continue;
                    string series = line.Substring(0, space).Trim();
                    string value = line.Substring(space + 1);
                    if (series.Contains(' ', StringComparison.Ordinal) && !series.Contains('{', StringComparison.Ordinal))
                    {
                        int s2 = series.LastIndexOf(' ');
                        value = series.Substring(s2 + 1);
                        series = series.Substring(0, s2);
                    }

                    if (Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) snap.Samples[series] = v;
                }

                snap.Available = true;
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !token.IsCancellationRequested))
            {
                snap.Available = false;
            }

            return snap;
        }

        /// <summary>
        /// Per-label sums and counts of a histogram's change between two snapshots.
        /// </summary>
        /// <param name="before">Earlier snapshot.</param>
        /// <param name="after">Later snapshot.</param>
        /// <param name="histogram">Histogram base name (for example mymorningnews_run_stage_duration_seconds).</param>
        /// <param name="label">Label to group by (for example stage).</param>
        /// <returns>Rows ordered by total time, largest first.</returns>
        public static List<StageTime> Diff(PrometheusSnapshot before, PrometheusSnapshot after, string histogram, string label)
        {
            ArgumentNullException.ThrowIfNull(before);
            ArgumentNullException.ThrowIfNull(after);
            Dictionary<string, StageTime> rows = new Dictionary<string, StageTime>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, double> kv in after.Samples)
            {
                bool isSum = kv.Key.StartsWith(histogram + "_sum", StringComparison.Ordinal);
                bool isCount = kv.Key.StartsWith(histogram + "_count", StringComparison.Ordinal);
                if (!isSum && !isCount) continue;
                string name = LabelValue(kv.Key, label) ?? "(all)";
                double prior = before.Samples.TryGetValue(kv.Key, out double p) ? p : 0;
                if (!rows.TryGetValue(name, out StageTime? row))
                {
                    row = new StageTime { Name = name };
                    rows[name] = row;
                }

                if (isSum) row.TotalSeconds += kv.Value - prior;
                else row.Count += (long)Math.Round(kv.Value - prior);
            }

            return rows.Values.Where(r => r.Count > 0).Select(r => { r.TotalSeconds = Math.Round(r.TotalSeconds, 4); r.MeanMs = Math.Round(r.TotalSeconds * 1000.0 / r.Count, 2); return r; })
                .OrderByDescending(r => r.TotalSeconds).ToList();
        }

        #endregion

        #region Private-Methods

        private static string? LabelValue(string series, string label)
        {
            int i = series.IndexOf(label + "=\"", StringComparison.Ordinal);
            if (i < 0) return null;
            int start = i + label.Length + 2;
            int end = series.IndexOf('"', start);
            return end < 0 ? null : series.Substring(start, end - start);
        }

        #endregion
    }
}
