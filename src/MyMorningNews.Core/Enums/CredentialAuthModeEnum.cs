namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// How a credential authenticates.
    /// </summary>
    public enum CredentialAuthModeEnum
    {
        /// <summary>
        /// Access key and secret sent as headers. The default; signed requests and session exchange also work.
        /// </summary>
        DirectHeader,

        /// <summary>
        /// HMAC-signed requests only: the secret never travels, so x-secret-key and session exchange are refused.
        /// </summary>
        SignedRequest,

        /// <summary>
        /// Exchanged for a session token at POST /v1.0/token only; per-request secrets and signatures are refused.
        /// </summary>
        SessionExchange,

        /// <summary>
        /// Any of the above (same as DirectHeader).
        /// </summary>
        Hybrid
    }
}
