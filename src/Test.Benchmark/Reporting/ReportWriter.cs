namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using MyMorningNews.Benchmark.Metrics;

    /// <summary>
    /// Writes a report as paired files with one base name: JSON for machines and Markdown for people, as
    /// {yyyyMMdd-HHmmss UTC}-{kind}-{dataset}[-{label}].json and .md, UTF-8 without a byte order mark.
    /// </summary>
    public static class ReportWriter
    {
        #region Public-Methods

        /// <summary>
        /// Write a report.
        /// </summary>
        /// <param name="report">Report.</param>
        /// <param name="directory">Output directory.</param>
        /// <returns>Path of the JSON file (the Markdown file has the same base name).</returns>
        public static string Write(BenchmarkReport report, string directory)
        {
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNull(directory);
            Directory.CreateDirectory(directory);
            string name = report.Provenance.StartUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + report.Kind + "-" + report.Dataset + (report.Label.Length > 0 ? "-" + report.Label : String.Empty);
            string json = Path.Combine(directory, name + ".json");
            Json.WriteFile(json, report);
            File.WriteAllText(Path.Combine(directory, name + ".md"), Markdown(report), new UTF8Encoding(false));
            return json;
        }

        /// <summary>
        /// Render a report as Markdown.
        /// </summary>
        /// <param name="report">Report.</param>
        /// <returns>Markdown.</returns>
        public static string Markdown(BenchmarkReport report)
        {
            ArgumentNullException.ThrowIfNull(report);
            StringBuilder md = new StringBuilder();
            md.Append("# ").Append(Title(report.Kind)).Append(": ").Append(report.Dataset).Append(report.Label.Length > 0 ? " (" + report.Label + ")" : String.Empty).Append("\n\n");
            if (report.Incomplete)
                md.Append("**Incomplete run: ").Append(report.IncompleteReasons.Count).Append(report.IncompleteReasons.Count == 1 ? " failure" : " failures").Append(". These results are not comparable.**\n\n")
                    .Append(String.Join("\n", report.IncompleteReasons.Select(r => "- " + r))).Append("\n\n");

            Provenance p = report.Provenance;
            md.Append("## Provenance\n\n| Field | Value |\n|---|---|\n");
            Row(md, "Start (UTC)", p.StartUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Row(md, "Mode", report.Mode + (report.Mode == "stub" ? " (deterministic stubs, no paid calls)" : " (paid model calls, spend capped)"));
            Row(md, "Server under test", p.SutUrl + " (version " + p.SutVersion + ")");
            Row(md, "Product commit", p.ProductCommit);
            Row(md, "Harness", p.HarnessVersion + " at " + p.HarnessCommit);
            Row(md, "Machine", p.Machine + ", " + p.Os + ", " + p.Cpu + ", " + p.Runtime);
            Row(md, "HTTP client", p.HttpClient);
            Row(md, "Dataset", p.Dataset + (p.DatasetSha256.Length > 0 ? " (sha256 " + p.DatasetSha256 + ")" : String.Empty));
            foreach (KeyValuePair<string, string> kv in p.Images) Row(md, "Image " + kv.Key, kv.Value);
            foreach (KeyValuePair<string, string> kv in p.Services) Row(md, "Service " + kv.Key, kv.Value);
            Row(md, "Command", "`" + p.CommandLine + "`");
            Row(md, "Duration", report.DurationSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s");
            md.Append('\n');

            md.Append("<details><summary>Options</summary>\n\n| Option | Value |\n|---|---|\n");
            foreach (KeyValuePair<string, string> kv in report.Options) Row(md, kv.Key, kv.Value);
            md.Append("\n</details>\n\n");

            if (report.Summaries.Count > 0)
            {
                md.Append("## Summary\n\nScores are means with 95% bootstrap intervals. Groups with fewer than 20 probes are marked *indicative*.\n\n");
                List<string> arms = report.Summaries.Select(s => s.Arm).Distinct(StringComparer.Ordinal).ToList();
                md.Append("| Group | Metric | ").Append(String.Join(" | ", arms)).Append(" |\n|---|---|").Append(String.Concat(arms.Select(a => "---|"))).Append('\n');
                foreach (string key in report.Summaries.Select(s => s.Group + "\u001f" + s.Metric).Distinct(StringComparer.Ordinal))
                {
                    string[] parts = key.Split('\u001f');
                    md.Append("| ").Append(parts[0]).Append(" | ").Append(parts[1]).Append(" |");
                    foreach (string arm in arms)
                    {
                        MetricSummary? s = report.Summaries.FirstOrDefault(x => x.Arm == arm && x.Group == parts[0] && x.Metric == parts[1]);
                        md.Append(' ').Append(s == null ? "n/a" : s.Value + " n=" + s.Value.N + (s.Value.Indicative ? " *indicative*" : String.Empty)).Append(" |");
                    }

                    md.Append('\n');
                }

                md.Append('\n');
            }

            DatasetBaselines baselines = DatasetBaselines.For(report.Dataset);
            md.Append("## Published baselines\n\n");
            if (baselines.Published.Count == 0) md.Append(baselines.Note.Length > 0 ? baselines.Note : "None.").Append("\n\n");
            else
            {
                md.Append("| Baseline | Metric | Published | Arm | This run | Net |\n|---|---|---|---|---|---|\n");
                foreach (PublishedBaseline b in baselines.Published)
                {
                    string[] gm = b.Metric.Split('/');
                    foreach (string arm in b.Arms)
                    {
                        MetricSummary? s = gm.Length == 2 ? Summarizer.Find(report, arm, gm[0], gm[1]) : null;
                        md.Append("| ").Append(b.Name).Append(" ([source](").Append(b.Url).Append(")) | ").Append(b.Metric).Append(" | ").Append(F(b.Value)).Append(" | ").Append(arm).Append(" | ")
                            .Append(s == null ? "n/a" : F(s.Value.Mean)).Append(" | ").Append(s == null ? "n/a" : Signed(s.Value.Mean - b.Value)).Append(" |\n");
                    }
                }

                md.Append('\n');
            }

            if (report.Stages.Count > 0)
            {
                md.Append("## Server stage breakdown\n\nFrom the server's own duration histograms, diffed before and after the measured phase. The counters are server-wide.\n\n| Stage | Count | Total s | Mean ms |\n|---|---|---|---|\n");
                foreach (StageTime s in report.Stages) md.Append("| ").Append(s.Name).Append(" | ").Append(s.Count).Append(" | ").Append(s.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(" | ").Append(s.MeanMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" |\n");
                md.Append('\n');
            }

            List<ProbeOutcome> timed = report.Probes.Where(x => x.Latency != null).ToList();
            if (timed.Count > 0)
            {
                md.Append("## Latency\n\nClient-side milliseconds over successful operations.\n\n| Arm | Probe | Count | Mean | p50 | p90 | p95 | p99 | Max | Other |\n|---|---|---|---|---|---|---|---|---|---|\n");
                foreach (ProbeOutcome t in timed)
                {
                    LatencyStats l = t.Latency!;
                    md.Append("| ").Append(t.Arm).Append(" | ").Append(t.ProbeId).Append(" | ").Append(l.Count).Append(" | ").Append(l.Mean).Append(" | ").Append(l.P50).Append(" | ").Append(l.P90).Append(" | ")
                        .Append(l.P95).Append(" | ").Append(l.P99).Append(" | ").Append(l.Max).Append(" | ")
                        .Append(String.Join(", ", t.Metrics.Select(kv => kv.Key + " " + kv.Value.ToString("0.###", CultureInfo.InvariantCulture)))).Append(" |\n");
                }

                md.Append('\n');
            }

            List<ProbeOutcome> misses = report.Probes.Where(x => x.Applicable && x.Score < 0.999).OrderBy(x => x.Score).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
            md.Append("## Misses\n\n");
            if (misses.Count == 0) md.Append("None: every applicable probe scored 1.\n\n");
            else
            {
                md.Append("| Arm | Corpus | Probe | Type | Score | Detail |\n|---|---|---|---|---|---|\n");
                foreach (ProbeOutcome m in misses.Take(40))
                    md.Append("| ").Append(m.Arm).Append(" | ").Append(m.Corpus).Append(" | ").Append(m.ProbeId).Append(" | ").Append(m.Type).Append(m.Negative ? " (negative)" : String.Empty).Append(" | ")
                        .Append(F(m.Score)).Append(" | ").Append(Cell(m.Error ?? m.Detail)).Append(" |\n");
                if (misses.Count > 40) md.Append("\n").Append(misses.Count - 40).Append(" more in the JSON report.\n");
                md.Append('\n');
            }

            if (report.Notes.Count > 0) md.Append("## Notes\n\n").Append(String.Join("\n", report.Notes.Select(n => "- " + n))).Append("\n\n");
            md.Append("Every per-probe outcome is in the JSON report with the same base name.\n");
            return md.ToString();
        }

        /// <summary>
        /// Format a number with three decimals.
        /// </summary>
        /// <param name="v">Value.</param>
        /// <returns>Text.</returns>
        public static string F(double v)
        {
            return v.ToString("0.000", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format a difference with a sign.
        /// </summary>
        /// <param name="v">Value.</param>
        /// <returns>Text.</returns>
        public static string Signed(double v)
        {
            return (v >= 0 ? "+" : String.Empty) + v.ToString("0.000", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Escape a table cell.
        /// </summary>
        /// <param name="s">Text.</param>
        /// <returns>Escaped text, at most 220 characters.</returns>
        public static string Cell(string s)
        {
            string t = (s ?? String.Empty).Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
            return t.Length > 220 ? t.Substring(0, 217) + "..." : t;
        }

        #endregion

        #region Private-Methods

        private static void Row(StringBuilder md, string field, string value)
        {
            md.Append("| ").Append(field).Append(" | ").Append((value ?? String.Empty).Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)).Append(" |\n");
        }

        private static string Title(string kind)
        {
            return kind switch
            {
                "quality" => "Quality",
                "ingest" => "Ingest",
                "e2e" => "End to end",
                "load" => "Load",
                "resilience" => "Resilience",
                _ => kind
            };
        }

        #endregion
    }
}
