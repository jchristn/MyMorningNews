namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// One stage of a run.
    /// </summary>
    public class RunStageDto
    {
        /// <summary>
        /// Stage name.
        /// </summary>
        public string Stage { get; set; } = String.Empty;

        /// <summary>
        /// Status.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Items in.
        /// </summary>
        public int ItemsIn { get; set; } = 0;

        /// <summary>
        /// Items out.
        /// </summary>
        public int ItemsOut { get; set; } = 0;

        /// <summary>
        /// Error or note, or null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Order.
        /// </summary>
        public int SortOrder { get; set; } = 0;
    }
}
