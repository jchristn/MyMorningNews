namespace MyMorningNews.Benchmark
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Runners;

    /// <summary>
    /// Entry point and command dispatch for the benchmark harness (BENCHMARKING.md). Exit codes: 0 success, 1 failure or
    /// regression, 2 usage, 3 incomplete run, 130 cancelled.
    /// </summary>
    public static class Program
    {
        #region Public-Methods

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            BenchmarkArguments parsed;
            try
            {
                parsed = new BenchmarkArguments(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Usage();
                return ExitCodes.Usage;
            }

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                try
                {
                    switch (parsed.Command)
                    {
                        case "validate":
                            return ValidateRunner.Run(parsed);
                        case "compare":
                            return CompareRunner.Run(parsed);
                        case "history":
                            return HistoryRunner.Run(parsed);
                        case "ingest":
                        case "quality":
                        case "e2e":
                        case "load":
                        case "resilience":
                        case "stub":
                            using (BenchmarkContext ctx = new BenchmarkContext(parsed))
                            {
                                return parsed.Command switch
                                {
                                    "ingest" => await QualityRunner.RunAsync(ctx, "ingest", cts.Token).ConfigureAwait(false),
                                    "quality" => await QualityRunner.RunAsync(ctx, "quality", cts.Token).ConfigureAwait(false),
                                    "e2e" => await E2eRunner.RunAsync(ctx, cts.Token).ConfigureAwait(false),
                                    "load" => await LoadRunner.RunAsync(ctx, cts.Token).ConfigureAwait(false),
                                    "resilience" => await ResilienceRunner.RunAsync(ctx, cts.Token).ConfigureAwait(false),
                                    _ => await StubRunner.RunAsync(ctx, cts.Token).ConfigureAwait(false)
                                };
                            }

                        case "":
                        case "help":
                            Usage();
                            return parsed.Command.Length == 0 ? ExitCodes.Usage : ExitCodes.Success;
                        default:
                            Console.Error.WriteLine("Unknown command '" + parsed.Command + "'.");
                            Usage();
                            return ExitCodes.Usage;
                    }
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    Console.Error.WriteLine("Cancelled.");
                    return ExitCodes.Cancelled;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
                {
                    Console.Error.WriteLine(ex.Message);
                    return ExitCodes.Usage;
                }
                catch (ModelGuardException ex)
                {
                    Console.Error.WriteLine("STOPPED: " + ex.Message);
                    return ExitCodes.Failure;
                }
                catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is System.IO.IOException || ex is System.Text.Json.JsonException || ex is InvalidOperationException || ex is System.Net.HttpListenerException)
                {
                    Console.Error.WriteLine("Error: " + ex.Message);
                    return ExitCodes.Failure;
                }
            }
        }

        #endregion

        #region Private-Methods

        private static void Usage()
        {
            Console.WriteLine(
                "MyMorningNews.Benchmark <command> [--option value ...]\n\n" +
                "Commands (benchmarks/README.md has every option):\n" +
                "  validate    Check dataset files (--dataset a.json,b.json)\n" +
                "  ingest      Provision workspaces, research every round, report the load path\n" +
                "  quality     Score every probe in every arm (--arms default,llm-only; reference arms included)\n" +
                "  e2e         Topic to run to briefing to email to audio to feedback link, with stage attribution (--repeat 3)\n" +
                "  load        API reads by concurrency (closed loop) or rate (--open-loop --rate 50), and concurrent runs\n" +
                "  resilience  Model, embeddings, search, and speech slow, down, or rate limited, and recovery\n" +
                "  compare     Paired, significance-gated diff of two reports (--baseline a.json --candidate b.json)\n" +
                "  history     Scores for every round side by side (--rounds R0=round0,R1=round1)\n" +
                "  stub        Run the stub model, search, pages, and speech on their own\n\n" +
                "Common options: --server http://127.0.0.1:12500 --metrics-url http://127.0.0.1:12509/metrics --stub-port 12503\n" +
                "  --label <round> --output-dir benchmarks/results --seed 20261120 --resamples 2000\n" +
                "Paid runs (never the default): --paid --max-spend-usd 1.00 --api-key-env <VAR> [--paid-provider Anthropic --paid-model claude-opus-5-5]\n\n" +
                "Exit codes: 0 success, 1 failure or regression, 2 usage, 3 incomplete run, 130 cancelled.");
        }

        #endregion
    }
}
