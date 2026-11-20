namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Client;
    using MyMorningNews.Benchmark.Datasets;

    /// <summary>
    /// Researches a corpus in a product arm: reuses the workspace when allowed and intact, otherwise provisions it and
    /// runs every round in order, then reads every round back. Shared by ingest and quality.
    /// </summary>
    public static class CorpusRunner
    {
        #region Public-Methods

        /// <summary>
        /// Research a corpus.
        /// </summary>
        /// <param name="provisioner">Provisioner.</param>
        /// <param name="index">Dataset index.</param>
        /// <param name="corpus">Corpus.</param>
        /// <param name="arm">Arm.</param>
        /// <param name="reingest">Ignore any saved workspace.</param>
        /// <param name="log">Progress output.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public static async Task<CorpusRun> RunAsync(Provisioner provisioner, DatasetIndex index, BenchmarkCorpus corpus, ProductArm arm, bool reingest, Action<string> log, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(provisioner);
            ArgumentNullException.ThrowIfNull(index);
            ArgumentNullException.ThrowIfNull(corpus);
            ArgumentNullException.ThrowIfNull(arm);
            ArgumentNullException.ThrowIfNull(log);
            CorpusRun result = new CorpusRun();
            string name = provisioner.NameFor(corpus, arm, String.Empty);
            Workspace? ws = reingest ? null : await provisioner.TryReuseAsync(name, corpus.Rounds, token).ConfigureAwait(false);
            if (ws != null)
            {
                result.Reused = true;
                log("  reusing workspace " + name + " (pass --reingest after changing code on the research path)");
            }
            else
            {
                log("  provisioning " + name);
                ws = await provisioner.CreateAsync(corpus, arm, String.Empty, token).ConfigureAwait(false);
                for (int round = 1; round <= corpus.Rounds; round++)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        RunDto run = await provisioner.RunRoundAsync(ws, corpus, round, token).ConfigureAwait(false);
                        result.RunSeconds[round] = Math.Round(sw.Elapsed.TotalSeconds, 2);
                        log("    round " + round + ": " + run.Status + " in " + result.RunSeconds[round].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s, " + run.NewFindingCount + " new, " + run.DuplicateCount + " duplicates, " + run.SourceCount + " sources");
                        if (run.Status != "Completed" && run.Status != "PartiallySucceeded")
                        {
                            result.Failures[round] = run.Status + ": " + (run.ErrorMessage ?? run.SkippedReason ?? "no message");
                            result.IncompleteReasons.Add(corpus.Id + "/" + arm.Name + " round " + round + " " + result.Failures[round]);
                        }
                    }
                    catch (TimeoutException ex)
                    {
                        result.Failures[round] = ex.Message;
                        result.IncompleteReasons.Add(corpus.Id + "/" + arm.Name + " round " + round + ": " + ex.Message);
                    }
                }

                result.IncompleteReasons.AddRange(ws.Notes.Select(n => corpus.Id + "/" + arm.Name + ": " + n));
            }

            result.Workspace = ws;
            MyMorningNewsClient client = await provisioner.SessionAsync(ws, token).ConfigureAwait(false);
            foreach (KeyValuePair<int, string> kv in ws.Runs)
            {
                if (result.Failures.ContainsKey(kv.Key)) continue;
                RoundData data = await RoundReader.ReadAsync(client, index, corpus.Id, kv.Key, kv.Value, token).ConfigureAwait(false);
                if (data.Run != null && data.Run.Status != "Completed" && data.Run.Status != "PartiallySucceeded")
                {
                    result.Failures[kv.Key] = data.Run.Status + ": " + (data.Run.ErrorMessage ?? "no message");
                    continue;
                }

                result.Rounds[kv.Key] = data;
                foreach (SearchResultDto r in data.Results.Where(r => r.Disposition == "FetchFailed" && data.ItemByResult.ContainsKey(r.Id)))
                {
                    BenchmarkItem? item = index.Item(corpus.Id, data.ItemByResult[r.Id]);
                    if (item != null && item.Url == null) result.IncompleteReasons.Add(corpus.Id + "/" + arm.Name + " round " + kv.Key + ": page " + item.Id + " failed to fetch");
                }
            }

            return result;
        }

        #endregion
    }
}
