namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Kind of stored secret.
    /// </summary>
    public enum VaultCredentialKindEnum
    {
        /// <summary>
        /// API key sent as the provider expects.
        /// </summary>
        ApiKey,

        /// <summary>
        /// Bearer token for an Authorization header.
        /// </summary>
        BearerToken,

        /// <summary>
        /// Username and password.
        /// </summary>
        BasicAuth,

        /// <summary>
        /// Value for a named custom header.
        /// </summary>
        CustomHeader,

        /// <summary>
        /// Connection string.
        /// </summary>
        ConnectionString
    }
}
