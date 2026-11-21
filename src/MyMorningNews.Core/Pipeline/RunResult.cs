namespace MyMorningNews.Core.Pipeline
{
    /// <summary>
    /// Outcome of executing a run.
    /// </summary>
    public class RunResult
    {
        /// <summary>
        /// Short outcome summary shown in run lists.
        /// </summary>
        public string? Summary { get; set; } = null;

        /// <summary>
        /// Briefing produced, when any.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Whether the run produced a deliverable (a briefing, report, or audio). One-time requests complete only when true.
        /// </summary>
        public bool Delivered { get; set; } = false;

        /// <summary>
        /// Whether a non-critical stage failed, or a stage finished with a warning such as Fetch reading no pages (the run
        /// finishes as PartiallySucceeded).
        /// </summary>
        public bool PartiallySucceeded { get; set; } = false;
    }
}
