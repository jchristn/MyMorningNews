namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// How a credential authenticates.
    /// </summary>
    public enum CredentialAuthModeEnum
    {
        /// <summary>
        /// Access key and secret sent as headers.
        /// </summary>
        DirectHeader,

        /// <summary>
        /// HMAC-signed requests.
        /// </summary>
        SignedRequest,

        /// <summary>
        /// Exchanged for a session token.
        /// </summary>
        SessionExchange,

        /// <summary>
        /// Any of the above.
        /// </summary>
        Hybrid
    }
}
