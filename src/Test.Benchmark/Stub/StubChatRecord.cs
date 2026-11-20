namespace MyMorningNews.Benchmark.Stub
{
    using System;

    /// <summary>
    /// One chat request the stub model received.
    /// </summary>
    public class StubChatRecord
    {
        /// <summary>
        /// Ledger tag when received (the workspace being run).
        /// </summary>
        public string Tag { get; set; } = String.Empty;

        /// <summary>
        /// Topic named in the prompt ("Topic: ..."), or empty.
        /// </summary>
        public string Topic { get; set; } = String.Empty;

        /// <summary>
        /// Prompt the stub recognized (extract, score, dedupe-judge, and so on), or "unknown".
        /// </summary>
        public string Prompt { get; set; } = String.Empty;

        /// <summary>
        /// Model name requested.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Whether the first system message starts with the expected scope guardrail.
        /// </summary>
        public bool Guardrail { get; set; } = false;

        /// <summary>
        /// Whether the request was streamed.
        /// </summary>
        public bool Streamed { get; set; } = false;

        /// <summary>
        /// HTTP status the stub answered with.
        /// </summary>
        public int Status { get; set; } = 200;

        /// <summary>
        /// UTC time received.
        /// </summary>
        public DateTime Utc { get; set; } = DateTime.UtcNow;
    }
}
