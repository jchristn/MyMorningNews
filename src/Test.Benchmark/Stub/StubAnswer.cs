namespace MyMorningNews.Benchmark.Stub
{
    using System;

    /// <summary>
    /// What the stub model answers: JSON arguments for structured prompts, or text for prose prompts.
    /// </summary>
    public class StubAnswer
    {
        /// <summary>
        /// Prompt the stub recognized.
        /// </summary>
        public string Prompt { get; set; } = "unknown";

        /// <summary>
        /// JSON arguments for structured prompts, or null for prose.
        /// </summary>
        public string? Arguments { get; set; } = null;

        /// <summary>
        /// Text for prose prompts.
        /// </summary>
        public string Text { get; set; } = "ready";

        /// <summary>
        /// Tool name for structured answers. Default submit_result.
        /// </summary>
        public string ToolName { get; set; } = "submit_result";
    }
}
