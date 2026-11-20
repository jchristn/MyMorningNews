namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Benchmark.Datasets;

    /// <summary>
    /// The validate command: checks one or more dataset files against the schema and their own consistency, and prints
    /// a summary of each (corpora, rounds, items, probes by type, negative probes, and the SHA-256).
    /// </summary>
    public static class ValidateRunner
    {
        #region Public-Methods

        /// <summary>
        /// Run the command.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code: 0 when every dataset is valid, 1 otherwise.</returns>
        public static int Run(BenchmarkArguments args)
        {
            ArgumentNullException.ThrowIfNull(args);
            List<string> paths = args.GetList("dataset", "benchmarks/datasets/mymorningnews-synthetic.json,benchmarks/datasets/mymorningnews-live.json");
            int failures = 0;
            foreach (string path in paths)
            {
                DatasetLoader loader;
                try
                {
                    loader = new DatasetLoader(path);
                }
                catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is System.Text.Json.JsonException)
                {
                    Console.WriteLine("INVALID " + path + ": " + ex.Message);
                    failures++;
                    continue;
                }

                List<string> problems = DatasetValidator.Validate(loader.Dataset);
                BenchmarkDataset d = loader.Dataset;
                List<BenchmarkProbe> probes = d.Corpora.SelectMany(c => c.Probes).ToList();
                Console.WriteLine((problems.Count == 0 ? "VALID   " : "INVALID ") + loader.Path);
                Console.WriteLine("  name " + d.Name + ", tier " + d.Tier + ", snapshot " + d.Snapshot + ", sha256 " + loader.Sha256);
                Console.WriteLine("  " + d.Corpora.Count + " corpora, " + d.Corpora.Sum(c => c.Rounds) + " rounds, " + d.Corpora.Sum(c => c.Items.Count) + " items, " + probes.Count + " probes (" + probes.Count(p => p.Negative) + " negative)");
                Console.WriteLine("  probes by type: " + String.Join(", ", probes.GroupBy(p => p.Type).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + " " + g.Count())));
                foreach (string p in problems) Console.WriteLine("  - " + p);
                if (problems.Count > 0) failures++;
            }

            return failures == 0 ? ExitCodes.Success : ExitCodes.Failure;
        }

        #endregion
    }
}
