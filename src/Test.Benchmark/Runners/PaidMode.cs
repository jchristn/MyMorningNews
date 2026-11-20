namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Settings for a paid run, where chat tasks go to a real model while search, pages, embeddings, and speech stay on
    /// the stub. Off unless --paid is given, and then only with a spend cap (--max-spend-usd) and an API key in the
    /// environment variable named by --api-key-env. The cap is enforced twice: the server refuses model calls once the
    /// reader's month-to-date cost reaches a plan cap the harness assigns, and the harness stops starting runs once the
    /// recorded cost reaches the cap.
    /// </summary>
    public class PaidMode
    {
        #region Public-Members

        /// <summary>
        /// Whether paid mode is on.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Provider, as the server names it (Anthropic or OpenAiCompatible). Default Anthropic.
        /// </summary>
        public string Provider { get; set; } = "Anthropic";

        /// <summary>
        /// Chat model. Default claude-opus-5-5.
        /// </summary>
        public string Model { get; set; } = "claude-opus-5-5";

        /// <summary>
        /// Endpoint base URL, or empty for the provider default.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// Reasoning effort, set explicitly. Default Low.
        /// </summary>
        public string ReasoningEffort { get; set; } = "Low";

        /// <summary>
        /// API key (never written to a report).
        /// </summary>
        public string ApiKey { get; set; } = String.Empty;

        /// <summary>
        /// Spend cap for the whole command in USD. Minimum 0.01, maximum 50.
        /// </summary>
        public decimal MaxSpendUsd
        {
            get { return _MaxSpendUsd; }
            set { _MaxSpendUsd = Math.Clamp(value, 0.01m, 50m); }
        }

        /// <summary>
        /// Input cost per million tokens, so the server can price calls. Default 5.
        /// </summary>
        public double InputCostPerMTok { get; set; } = 5.0;

        /// <summary>
        /// Output cost per million tokens. Default 25.
        /// </summary>
        public double OutputCostPerMTok { get; set; } = 25.0;

        #endregion

        #region Private-Members

        private decimal _MaxSpendUsd = 1m;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Read paid settings from the arguments. Returns a disabled instance unless --paid is given.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Settings.</returns>
        /// <exception cref="ArgumentException">--paid was given without a spend cap or without an API key.</exception>
        public static PaidMode From(BenchmarkArguments args)
        {
            ArgumentNullException.ThrowIfNull(args);
            PaidMode p = new PaidMode { Enabled = args.GetFlag("paid") };
            if (!p.Enabled) return p;
            string? cap = args.GetOptional("max-spend-usd");
            if (cap == null || !Decimal.TryParse(cap, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal capValue) || capValue <= 0)
                throw new ArgumentException("--paid needs --max-spend-usd with a positive amount (at most 50). Nothing was run.");
            p.MaxSpendUsd = capValue;
            p.Provider = args.GetString("paid-provider", p.Provider);
            p.Model = args.GetString("paid-model", p.Model);
            p.Endpoint = args.GetString("paid-endpoint", p.Endpoint);
            p.ReasoningEffort = args.GetString("paid-reasoning-effort", p.ReasoningEffort);
            p.InputCostPerMTok = args.GetDouble("paid-input-cost-per-mtok", p.InputCostPerMTok, 0, 1000);
            p.OutputCostPerMTok = args.GetDouble("paid-output-cost-per-mtok", p.OutputCostPerMTok, 0, 1000);
            string variable = args.GetString("api-key-env", "MYMORNINGNEWS_BENCH_PAID_API_KEY");
            p.ApiKey = Environment.GetEnvironmentVariable(variable) ?? String.Empty;
            if (p.ApiKey.Length == 0) throw new ArgumentException("--paid needs an API key in the environment variable " + variable + ". Nothing was run.");
            return p;
        }

        #endregion
    }
}
