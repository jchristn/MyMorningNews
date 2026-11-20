namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using MyMorningNews.Benchmark.Reporting;

    /// <summary>
    /// The history command: one table per dataset and arm with the headline score overall, for negative probes, and for
    /// every probe type, one column per round, the net change from the first round to the last, the probe count per
    /// type, and the published baselines. Includes the end-to-end and resilience scores per round when those reports
    /// exist under the same labels. Round selectors are run labels or UTC timestamp prefixes
    /// (--rounds R0=round0,R1=20261121). Saves {utc}-history.md next to the reports.
    /// </summary>
    public static class HistoryRunner
    {
        #region Public-Methods

        /// <summary>
        /// Run the command.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        public static int Run(BenchmarkArguments args)
        {
            ArgumentNullException.ThrowIfNull(args);
            List<string> selectors = args.GetList("rounds", String.Empty);
            string dir = RepositoryPaths.Resolve(args.GetString("dir", RepositoryPaths.Results()));
            List<string> armFilter = args.GetList("arms", String.Empty);
            if (selectors.Count == 0)
            {
                Console.Error.WriteLine("history needs --rounds R0=<label or utc prefix>,R1=...");
                return ExitCodes.Usage;
            }

            List<KeyValuePair<string, string>> rounds = selectors.Select(s => s.Contains('=') ? new KeyValuePair<string, string>(s.Split('=')[0], s.Split('=')[1]) : new KeyValuePair<string, string>(s, s)).ToList();
            List<KeyValuePair<string, BenchmarkReport>> all = new List<KeyValuePair<string, BenchmarkReport>>();
            foreach (string file in Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal) : Enumerable.Empty<string>())
            {
                string name = Path.GetFileName(file);
                if (name.Contains("-compare-", StringComparison.Ordinal)) continue;
                try
                {
                    BenchmarkReport r = Json.Deserialize<BenchmarkReport>(File.ReadAllText(file));
                    if (r.Kind.Length > 0) all.Add(new KeyValuePair<string, BenchmarkReport>(name, r));
                }
                catch (System.Text.Json.JsonException)
                {
                }
            }

            StringBuilder md = new StringBuilder("# Benchmark history\n\nRounds: " + String.Join(", ", rounds.Select(r => r.Key + " = " + r.Value)) + ". Scores are means; types with fewer than 20 probes are indicative.\n\n");
            foreach (string dataset in all.Where(r => r.Value.Kind == "quality").Select(r => r.Value.Dataset).Distinct(StringComparer.Ordinal).OrderBy(d => d, StringComparer.Ordinal))
            {
                List<KeyValuePair<string, KeyValuePair<string, BenchmarkReport>?>> columns = rounds.Select(r => new KeyValuePair<string, KeyValuePair<string, BenchmarkReport>?>(r.Key, Pick(all, "quality", dataset, r.Value))).ToList();
                if (columns.All(c => c.Value == null)) continue;
                md.Append("## ").Append(dataset).Append("\n\n");
                foreach (KeyValuePair<string, KeyValuePair<string, BenchmarkReport>?> col in columns)
                    md.Append("- ").Append(col.Key).Append(": ").Append(col.Value == null ? "no report" : "`" + col.Value.Value.Key + "`" + (col.Value.Value.Value.Incomplete ? " (**incomplete**)" : String.Empty)).Append('\n');
                md.Append('\n');
                List<string> arms = columns.Where(c => c.Value != null).SelectMany(c => c.Value!.Value.Value.Arms).Distinct(StringComparer.Ordinal).Where(a => armFilter.Count == 0 || armFilter.Contains(a)).ToList();
                DatasetBaselines baselines = DatasetBaselines.For(dataset);
                foreach (string arm in arms)
                {
                    md.Append("### ").Append(arm).Append("\n\n| Group | Metric | n | ").Append(String.Join(" | ", columns.Select(c => c.Key))).Append(" | Net |\n|---|---|---|").Append(String.Concat(columns.Select(c => "---|"))).Append("---|\n");
                    List<KeyValuePair<string, string>> groups = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("overall", "score"), new KeyValuePair<string, string>("negative", "score") };
                    foreach (string g in columns.Where(c => c.Value != null).SelectMany(c => c.Value!.Value.Value.Summaries.Where(s => s.Arm == arm && s.Group.StartsWith("type:", StringComparison.Ordinal) && s.Metric == "score").Select(s => s.Group)).Distinct(StringComparer.Ordinal).OrderBy(g => g, StringComparer.Ordinal))
                        groups.Add(new KeyValuePair<string, string>(g, "score"));
                    groups.Add(new KeyValuePair<string, string>("type:ranking", "ndcg10"));
                    foreach (KeyValuePair<string, string> g in groups)
                    {
                        List<MetricSummary?> values = columns.Select(c => c.Value == null ? null : Summarizer.Find(c.Value.Value.Value, arm, g.Key, g.Value)).ToList();
                        if (values.All(v => v == null)) continue;
                        MetricSummary? last = values.LastOrDefault(v => v != null);
                        MetricSummary? first = values.FirstOrDefault(v => v != null);
                        int n = last?.Value.N ?? 0;
                        md.Append("| ").Append(g.Key).Append(" | ").Append(g.Value).Append(" | ").Append(n).Append(n < 20 ? " *ind.*" : String.Empty).Append(" | ")
                            .Append(String.Join(" | ", values.Select(v => v == null ? "n/a" : ReportWriter.F(v.Value.Mean))))
                            .Append(" | ").Append(first != null && last != null ? ReportWriter.Signed(last.Value.Mean - first.Value.Mean) : "n/a").Append(" |\n");
                    }

                    md.Append('\n');
                }

                md.Append("Published baselines: ");
                if (baselines.Published.Count == 0) md.Append(baselines.Note.Length > 0 ? baselines.Note : "none").Append("\n\n");
                else
                {
                    KeyValuePair<string, BenchmarkReport>? latest = columns.LastOrDefault(c => c.Value != null).Value;
                    md.Append('\n');
                    foreach (PublishedBaseline b in baselines.Published)
                    {
                        string[] gm = b.Metric.Split('/');
                        foreach (string arm in b.Arms)
                        {
                            MetricSummary? s = latest == null || gm.Length != 2 ? null : Summarizer.Find(latest.Value.Value, arm, gm[0], gm[1]);
                            md.Append("- ").Append(b.Name).Append(" ").Append(b.Metric).Append(" ").Append(ReportWriter.F(b.Value)).Append(" vs ").Append(arm).Append(" ")
                                .Append(s == null ? "n/a" : ReportWriter.F(s.Value.Mean) + " (net " + ReportWriter.Signed(s.Value.Mean - b.Value) + ")").Append(" [").Append(b.Source).Append("](").Append(b.Url).Append(")\n");
                        }
                    }

                    md.Append('\n');
                }
            }

            foreach (string kind in new[] { "e2e", "resilience", "ingest" })
            {
                List<KeyValuePair<string, KeyValuePair<string, BenchmarkReport>?>> columns = rounds.Select(r => new KeyValuePair<string, KeyValuePair<string, BenchmarkReport>?>(r.Key, Pick(all, kind, null, r.Value))).ToList();
                if (columns.All(c => c.Value == null)) continue;
                md.Append("## ").Append(kind).Append("\n\n| Arm | Group | ").Append(String.Join(" | ", columns.Select(c => c.Key))).Append(" | Net |\n|---|---|").Append(String.Concat(columns.Select(c => "---|"))).Append("---|\n");
                foreach (string key in columns.Where(c => c.Value != null).SelectMany(c => c.Value!.Value.Value.Summaries.Where(s => s.Metric == "score" && (s.Group == "overall" || s.Group.StartsWith("type:", StringComparison.Ordinal))).Select(s => s.Arm + "\u001f" + s.Group)).Distinct(StringComparer.Ordinal))
                {
                    string[] parts = key.Split('\u001f');
                    List<MetricSummary?> values = columns.Select(c => c.Value == null ? null : Summarizer.Find(c.Value.Value.Value, parts[0], parts[1], "score")).ToList();
                    MetricSummary? first = values.FirstOrDefault(v => v != null);
                    MetricSummary? last = values.LastOrDefault(v => v != null);
                    md.Append("| ").Append(parts[0]).Append(" | ").Append(parts[1]).Append(" | ").Append(String.Join(" | ", values.Select(v => v == null ? "n/a" : ReportWriter.F(v.Value.Mean))))
                        .Append(" | ").Append(first != null && last != null ? ReportWriter.Signed(last.Value.Mean - first.Value.Mean) : "n/a").Append(" |\n");
                }

                md.Append("\nReports: ").Append(String.Join(", ", columns.Where(c => c.Value != null).Select(c => c.Key + " `" + c.Value!.Value.Key + "`"))).Append("\n\n");
            }

            string path = Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-history.md");
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, md.ToString(), new UTF8Encoding(false));
            Console.WriteLine(md.ToString());
            Console.WriteLine("Saved " + path);
            return ExitCodes.Success;
        }

        #endregion

        #region Private-Methods

        private static KeyValuePair<string, BenchmarkReport>? Pick(List<KeyValuePair<string, BenchmarkReport>> all, string kind, string? dataset, string selector)
        {
            List<KeyValuePair<string, BenchmarkReport>> matches = all.Where(r => r.Value.Kind == kind && (dataset == null || r.Value.Dataset == dataset) &&
                (String.Equals(r.Value.Label, selector, StringComparison.Ordinal) || r.Key.StartsWith(selector, StringComparison.Ordinal))).ToList();
            if (matches.Count == 0) return null;
            return matches.OrderBy(r => r.Key, StringComparer.Ordinal).Last();
        }

        #endregion
    }
}
