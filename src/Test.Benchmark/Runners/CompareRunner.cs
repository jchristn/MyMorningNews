namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using MyMorningNews.Benchmark.Metrics;
    using MyMorningNews.Benchmark.Reporting;

    /// <summary>
    /// The compare command, the regression gate. Pairs probes by arm/corpus/probe and compares the primary score and every
    /// secondary metric per arm, overall and per type. A metric regresses when the mean drops by more than --tolerance
    /// (default 0.01) and a paired bootstrap says the drop is real (interval below zero, or p below --alpha, default 0.05).
    /// With --deterministic (the CI gate, whose runs are fully stub-backed) any single probe that gets worse is a
    /// regression, because there is no noise to forgive. Latency p95 and throughput are compared only when
    /// --latency-tolerance is given (same machine only). Mismatched probe sets and incomplete reports fail unless
    /// --allow-partial. The table is printed and saved as {utc}-compare-{name}.md and .json; the last line is
    /// "RESULT: regression" or "RESULT: no regression". Works for every report kind.
    /// </summary>
    public static class CompareRunner
    {
        #region Public-Methods

        /// <summary>
        /// Run the command.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>0 with no regression, 1 on regression or failure, 2 on bad usage.</returns>
        public static int Run(BenchmarkArguments args)
        {
            ArgumentNullException.ThrowIfNull(args);
            string? baselinePath = args.GetOptional("baseline");
            string? candidatePath = args.GetOptional("candidate");
            if (baselinePath == null || candidatePath == null)
            {
                Console.Error.WriteLine("compare needs --baseline <report.json> and --candidate <report.json>.");
                return ExitCodes.Usage;
            }

            double tolerance = args.GetDouble("tolerance", 0.01, 0, 1);
            double alpha = args.GetDouble("alpha", 0.05, 0.0001, 0.5);
            string? latencyRaw = args.GetOptional("latency-tolerance");
            double? latencyTolerance = latencyRaw == null || latencyRaw == "none" ? null : Double.Parse(latencyRaw, CultureInfo.InvariantCulture);
            bool deterministic = args.GetFlag("deterministic");
            bool allowPartial = args.GetFlag("allow-partial");
            int resamples = args.GetInt("resamples", 2000, 100, 100000);
            int seed = args.GetInt("seed", 20261120, Int32.MinValue, Int32.MaxValue);
            string basePath = RepositoryPaths.Resolve(baselinePath);
            string candPath = RepositoryPaths.Resolve(candidatePath);
            BenchmarkReport baseline = Json.Deserialize<BenchmarkReport>(File.ReadAllText(basePath));
            BenchmarkReport candidate = Json.Deserialize<BenchmarkReport>(File.ReadAllText(candPath));

            CompareReport result = Compare(baseline, candidate, tolerance, alpha, latencyTolerance, deterministic, allowPartial, resamples, seed);
            result.Baseline = basePath;
            result.Candidate = candPath;
            string outDir = RepositoryPaths.Resolve(args.GetString("output-dir", Path.GetDirectoryName(candPath) ?? RepositoryPaths.Results()));
            string name = args.GetString("name", candidate.Kind + "-" + candidate.Dataset + (candidate.Label.Length > 0 ? "-" + candidate.Label : String.Empty));
            string stem = Path.Combine(outDir, result.CreatedUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-compare-" + name);
            string md = Markdown(result);
            Json.WriteFile(stem + ".json", result);
            File.WriteAllText(stem + ".md", md, new UTF8Encoding(false));
            Console.WriteLine(md);
            Console.WriteLine("Saved " + stem + ".md");
            Console.WriteLine(result.Regression ? "RESULT: regression" : "RESULT: no regression");
            return result.Regression ? ExitCodes.Failure : ExitCodes.Success;
        }

        /// <summary>
        /// Compare two reports.
        /// </summary>
        /// <param name="baseline">Baseline.</param>
        /// <param name="candidate">Candidate.</param>
        /// <param name="tolerance">Score tolerance.</param>
        /// <param name="alpha">Significance level.</param>
        /// <param name="latencyTolerance">Relative latency tolerance, or null to skip latency.</param>
        /// <param name="deterministic">Any per-probe decrease is a regression.</param>
        /// <param name="allowPartial">Allow mismatched probe sets and incomplete reports.</param>
        /// <param name="resamples">Bootstrap resamples.</param>
        /// <param name="seed">Bootstrap seed.</param>
        /// <returns>Result.</returns>
        public static CompareReport Compare(BenchmarkReport baseline, BenchmarkReport candidate, double tolerance, double alpha, double? latencyTolerance, bool deterministic, bool allowPartial, int resamples, int seed)
        {
            ArgumentNullException.ThrowIfNull(baseline);
            ArgumentNullException.ThrowIfNull(candidate);
            CompareReport r = new CompareReport { Kind = candidate.Kind };
            r.Settings["tolerance"] = tolerance.ToString("0.####", CultureInfo.InvariantCulture);
            r.Settings["alpha"] = alpha.ToString("0.####", CultureInfo.InvariantCulture);
            r.Settings["latencyTolerance"] = latencyTolerance?.ToString("0.###", CultureInfo.InvariantCulture) ?? "off";
            r.Settings["mode"] = deterministic ? "deterministic (any per-probe decrease regresses)" : "tolerance and significance";
            if (baseline.Kind != candidate.Kind) r.Problems.Add("Report kinds differ: " + baseline.Kind + " and " + candidate.Kind + ".");
            if (baseline.Dataset != candidate.Dataset) r.Problems.Add("Datasets differ: " + baseline.Dataset + " and " + candidate.Dataset + ".");
            else if (baseline.Provenance.DatasetSha256 != candidate.Provenance.DatasetSha256 && !allowPartial) r.Problems.Add("The dataset file changed between the reports (sha256 differs); pass --allow-partial to compare anyway.");
            if (baseline.Incomplete && !allowPartial) r.Problems.Add("The baseline is an incomplete run and is not comparable.");
            if (candidate.Incomplete && !allowPartial) r.Problems.Add("The candidate is an incomplete run and is not comparable.");

            Dictionary<string, ProbeOutcome> b = baseline.Probes.ToDictionary(p => p.Key, p => p, StringComparer.Ordinal);
            Dictionary<string, ProbeOutcome> c = candidate.Probes.ToDictionary(p => p.Key, p => p, StringComparer.Ordinal);
            r.MissingFromCandidate = b.Keys.Where(k => !c.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            r.MissingFromBaseline = c.Keys.Where(k => !b.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if ((r.MissingFromCandidate.Count > 0 || r.MissingFromBaseline.Count > 0) && !allowPartial)
                r.Problems.Add("Probe sets differ: " + r.MissingFromCandidate.Count + " only in the baseline, " + r.MissingFromBaseline.Count + " only in the candidate. Pass --allow-partial to compare the shared probes.");

            List<string> shared = b.Keys.Where(c.ContainsKey).OrderBy(k => k, StringComparer.Ordinal).ToList();
            foreach (string arm in shared.Select(k => b[k].Arm).Distinct(StringComparer.Ordinal))
            {
                List<string> armKeys = shared.Where(k => b[k].Arm == arm && b[k].Applicable && c[k].Applicable).ToList();
                AddRow(r, arm, "overall", "score", armKeys, b, c, k => k.Score, tolerance, alpha, deterministic, resamples, seed);
                foreach (string type in armKeys.Select(k => b[k].Type).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal))
                {
                    List<string> typeKeys = armKeys.Where(k => b[k].Type == type).ToList();
                    AddRow(r, arm, "type:" + type, "score", typeKeys, b, c, k => k.Score, tolerance, alpha, deterministic, resamples, seed);
                    foreach (string metric in typeKeys.SelectMany(k => b[k].Metrics.Keys).Distinct(StringComparer.Ordinal).Where(m => !IsTiming(m)).OrderBy(m => m, StringComparer.Ordinal))
                    {
                        List<string> mk = typeKeys.Where(k => b[k].Metrics.ContainsKey(metric) && c[k].Metrics.ContainsKey(metric)).ToList();
                        AddRow(r, arm, "type:" + type, metric, mk, b, c, k => k.Metrics[metric], tolerance, alpha, deterministic, resamples, seed);
                    }
                }

                if (latencyTolerance.HasValue)
                {
                    foreach (string k in shared.Where(k => b[k].Arm == arm && b[k].Latency != null && c[k].Latency != null))
                    {
                        double bp = b[k].Latency!.P95;
                        double cp = c[k].Latency!.P95;
                        double rel = bp <= 0 ? 0 : (cp - bp) / bp;
                        r.Rows.Add(new CompareRow { Arm = arm, Group = b[k].ProbeId, Metric = "p95Ms", N = c[k].Latency!.Count, Baseline = bp, Candidate = cp, Delta = rel, Regression = rel > latencyTolerance.Value, Improvement = rel < -latencyTolerance.Value });
                        if (b[k].Metrics.TryGetValue("throughput", out double bt) && c[k].Metrics.TryGetValue("throughput", out double ct) && bt > 0)
                        {
                            double tr = (ct - bt) / bt;
                            r.Rows.Add(new CompareRow { Arm = arm, Group = b[k].ProbeId, Metric = "throughput", N = 1, Baseline = bt, Candidate = ct, Delta = tr, Regression = tr < -latencyTolerance.Value, Improvement = tr > latencyTolerance.Value });
                        }
                    }
                }
            }

            r.Regression = r.Problems.Count > 0 || r.Rows.Any(x => x.Regression);
            return r;
        }

        #endregion

        #region Private-Methods

        private static readonly HashSet<string> _Rates = new HashSet<string>(StringComparer.Ordinal) { "ndcg10", "p5", "shownPrecision", "recall", "evidencePrecision", "precision", "linked", "reachability" };

        private static bool IsTiming(string metric)
        {
            return !_Rates.Contains(metric);
        }

        private static void AddRow(CompareReport r, string arm, string group, string metric, List<string> keys, Dictionary<string, ProbeOutcome> b, Dictionary<string, ProbeOutcome> c,
            Func<ProbeOutcome, double> value, double tolerance, double alpha, bool deterministic, int resamples, int seed)
        {
            if (keys.Count == 0) return;
            List<double> bv = keys.Select(k => value(b[k])).ToList();
            List<double> cv = keys.Select(k => value(c[k])).ToList();
            PairedResult paired = Bootstrap.Paired(bv, cv, resamples, seed);
            bool significant = paired.High < 0 || paired.P < alpha;
            CompareRow row = new CompareRow
            {
                Arm = arm,
                Group = group,
                Metric = metric,
                N = keys.Count,
                Baseline = bv.Average(),
                Candidate = cv.Average(),
                Delta = paired.Delta,
                Low = paired.Low,
                High = paired.High,
                P = paired.P
            };
            if (deterministic)
            {
                row.Worse = keys.Where(k => value(c[k]) < value(b[k]) - 1e-9).ToList();
                row.Regression = row.Worse.Count > 0 || paired.Delta < -tolerance;
            }
            else row.Regression = paired.Delta < -tolerance && significant;
            row.Improvement = paired.Delta > tolerance && (paired.Low > 0 || paired.P < alpha);
            r.Rows.Add(row);
        }

        private static string Markdown(CompareReport r)
        {
            StringBuilder md = new StringBuilder();
            md.Append("# Compare: ").Append(r.Kind).Append("\n\n- Baseline: `").Append(r.Baseline).Append("`\n- Candidate: `").Append(r.Candidate).Append("`\n");
            foreach (KeyValuePair<string, string> kv in r.Settings) md.Append("- ").Append(kv.Key).Append(": ").Append(kv.Value).Append('\n');
            md.Append('\n');
            if (r.Problems.Count > 0) md.Append("**Problems**\n\n").Append(String.Join("\n", r.Problems.Select(p => "- " + p))).Append("\n\n");
            md.Append("| Arm | Group | Metric | n | Baseline | Candidate | Delta | 95% CI | p | Verdict |\n|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (CompareRow row in r.Rows)
            {
                string verdict = row.Regression ? "**regression**" + (row.Worse.Count > 0 ? " (" + row.Worse.Count + " worse: " + String.Join(", ", row.Worse.Take(5)) + (row.Worse.Count > 5 ? ", ..." : String.Empty) + ")" : String.Empty) : row.Improvement ? "improvement" : "same";
                md.Append("| ").Append(row.Arm).Append(" | ").Append(row.Group).Append(" | ").Append(row.Metric).Append(" | ").Append(row.N).Append(" | ").Append(ReportWriter.F(row.Baseline)).Append(" | ").Append(ReportWriter.F(row.Candidate))
                    .Append(" | ").Append(ReportWriter.Signed(row.Delta)).Append(" | [").Append(ReportWriter.F(row.Low)).Append(", ").Append(ReportWriter.F(row.High)).Append("] | ").Append(row.P.ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(" | ").Append(verdict).Append(" |\n");
            }

            if (r.MissingFromCandidate.Count > 0) md.Append("\nOnly in the baseline: ").Append(String.Join(", ", r.MissingFromCandidate.Take(20))).Append(r.MissingFromCandidate.Count > 20 ? ", ..." : String.Empty).Append('\n');
            if (r.MissingFromBaseline.Count > 0) md.Append("\nOnly in the candidate: ").Append(String.Join(", ", r.MissingFromBaseline.Take(20))).Append(r.MissingFromBaseline.Count > 20 ? ", ..." : String.Empty).Append('\n');
            return md.ToString();
        }

        #endregion
    }
}
