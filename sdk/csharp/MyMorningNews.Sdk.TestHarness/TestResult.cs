namespace MyMorningNews.Sdk.TestHarness
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of one harness case.
    /// </summary>
    public class TestResult
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Outcome. Default Pass.
        /// </summary>
        public TestOutcomeEnum Outcome { get; set; } = TestOutcomeEnum.Pass;

        /// <summary>
        /// Failure or skip reason, or null.
        /// </summary>
        public string? Message { get; set; } = null;

        /// <summary>
        /// Start time (UTC).
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End time (UTC).
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Steps run by the case.
        /// </summary>
        public List<ApiDetails> ApiDetails { get; set; } = new List<ApiDetails>();

        /// <summary>
        /// Elapsed milliseconds.
        /// </summary>
        public long ElapsedMs
        {
            get
            {
                return (long)(EndUtc - StartUtc).TotalMilliseconds;
            }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TestResult()
        {
        }
    }
}
