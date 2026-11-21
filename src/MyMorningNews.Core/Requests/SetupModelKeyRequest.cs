namespace MyMorningNews.Core.Requests
{
    /// <summary>
    /// Store an API key for a model endpoint and verify it.
    /// </summary>
    public class SetupModelKeyRequest
    {
        /// <summary>
        /// Model endpoint. Required.
        /// </summary>
        [TextLimit(64)]
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// API key. Required; stored encrypted in the vault and never returned.
        /// </summary>
        [TextLimit(4096)]
        public string? ApiKey { get; set; } = null;
    }
}
