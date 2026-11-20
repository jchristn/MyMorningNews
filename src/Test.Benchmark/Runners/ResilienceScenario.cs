namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using MyMorningNews.Benchmark.Stub;

    /// <summary>
    /// One injected failure and the behavior a reader would want from the product.
    /// </summary>
    public class ResilienceScenario
    {
        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Dependency the fault hits.
        /// </summary>
        public FaultTarget Target { get; set; } = FaultTarget.Model;

        /// <summary>
        /// Fault.
        /// </summary>
        public FaultMode Mode { get; set; } = FaultMode.None;

        /// <summary>
        /// Expected run statuses, comma separated (Completed, PartiallySucceeded, Failed).
        /// </summary>
        public string ExpectStatus { get; set; } = "Completed";

        /// <summary>
        /// Whether the run must still produce shown findings.
        /// </summary>
        public bool ExpectFindings { get; set; } = true;

        /// <summary>
        /// Whether the fault applies to audio (made after a healthy run) instead of the run.
        /// </summary>
        public bool Audio { get; set; } = false;

        /// <summary>
        /// Expected audio status when Audio is set (Ready or Failed).
        /// </summary>
        public string ExpectAudio { get; set; } = "Ready";

        /// <summary>
        /// Whether to clear the fault and run the same topic again, expecting it to recover the findings.
        /// </summary>
        public bool Recovery { get; set; } = false;

        /// <summary>
        /// Whether to make a dry run first; the real run must then show as much as a healthy run.
        /// </summary>
        public bool DryRunFirst { get; set; } = false;

        /// <summary>
        /// What the scenario checks.
        /// </summary>
        public string Description { get; set; } = String.Empty;
    }
}
