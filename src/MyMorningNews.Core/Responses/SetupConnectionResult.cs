namespace MyMorningNews.Core.Responses
{
    /// <summary>
    /// Outcome of a Setup page connection attempt.
    /// </summary>
    public class SetupConnectionResult
    {
        /// <summary>
        /// Endpoint that was connected or tried.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Whether the live test prompt succeeded.
        /// </summary>
        public bool Succeeded { get; set; } = false;

        /// <summary>
        /// Whether a working model was kept because the new one failed its test. The workspace keeps using it.
        /// </summary>
        public bool KeptPrevious { get; set; } = false;

        /// <summary>
        /// The test's result in plain language (the provider's error text on failure).
        /// </summary>
        public string? Message { get; set; } = null;
    }
}
