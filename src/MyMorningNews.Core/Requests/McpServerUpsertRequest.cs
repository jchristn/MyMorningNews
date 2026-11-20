namespace MyMorningNews.Core.Requests
{
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Register or change an MCP server. On update, null properties are unchanged.
    /// </summary>
    public class McpServerUpsertRequest
    {
        /// <summary>
        /// Display name (required on create, at most 128 characters).
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Endpoint URL, http or https (required on create).
        /// </summary>
        public string? Url { get; set; } = null;

        /// <summary>
        /// Transport. Default StreamableHttp.
        /// </summary>
        public McpTransportEnum? Transport { get; set; } = null;

        /// <summary>
        /// Vault credential whose value is sent in the auth header. An empty string clears it.
        /// </summary>
        public string? VaultCredentialId { get; set; } = null;

        /// <summary>
        /// Header for the credential. Default Authorization.
        /// </summary>
        public string? HeaderName { get; set; } = null;

        /// <summary>
        /// Text placed before the credential value. Default "Bearer ".
        /// </summary>
        public string? HeaderPrefix { get; set; } = null;

        /// <summary>
        /// Whether its tools may be used.
        /// </summary>
        public bool? Enabled { get; set; } = null;
    }
}
