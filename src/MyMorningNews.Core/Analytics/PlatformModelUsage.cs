namespace MyMorningNews.Core.Analytics
{
    using System;

    /// <summary>
    /// Model usage for one provider and model over a period.
    /// </summary>
    public class PlatformModelUsage
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Number of calls.
        /// </summary>
        public long Calls { get; set; } = 0;

        /// <summary>
        /// Input plus output tokens.
        /// </summary>
        public long Tokens { get; set; } = 0;

        /// <summary>
        /// Estimated cost in US dollars.
        /// </summary>
        public decimal CostUsd { get; set; } = 0m;
    }
}
