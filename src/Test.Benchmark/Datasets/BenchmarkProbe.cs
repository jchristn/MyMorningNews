namespace MyMorningNews.Benchmark.Datasets
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One probe: a measured outcome with a known correct answer. Types are listed in ProbeTypes and SCHEMA.md.
    /// </summary>
    public class BenchmarkProbe
    {
        /// <summary>
        /// Stable identifier, unique in the corpus.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Probe type (what makes it hard).
        /// </summary>
        public string Type { get; set; } = String.Empty;

        /// <summary>
        /// Category (the domain area it tests).
        /// </summary>
        public string Category { get; set; } = String.Empty;

        /// <summary>
        /// Round the probe is scored in. Default 1.
        /// </summary>
        public int Round { get; set; } = 1;

        /// <summary>
        /// Whether the correct outcome is "nothing" (no finding, no fetch, an empty result).
        /// </summary>
        public bool Negative { get; set; } = false;

        /// <summary>
        /// Subject item, or null.
        /// </summary>
        public string? Item { get; set; } = null;

        /// <summary>
        /// Related item: the original of a duplicate, update, or alias. Null when not used.
        /// </summary>
        public string? Of { get; set; } = null;

        /// <summary>
        /// Items of a duplicate cluster.
        /// </summary>
        public List<string> Items { get; set; } = new List<string>();

        /// <summary>
        /// For order probes: the item that must rank higher.
        /// </summary>
        public string? Above { get; set; } = null;

        /// <summary>
        /// For order probes: the item that must rank lower.
        /// </summary>
        public string? Below { get; set; } = null;

        /// <summary>
        /// Note on what the probe checks.
        /// </summary>
        public string? Note { get; set; } = null;
    }
}
