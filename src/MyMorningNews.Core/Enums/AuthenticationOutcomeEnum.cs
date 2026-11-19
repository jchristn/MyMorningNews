namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Authentication outcome.
    /// </summary>
    public enum AuthenticationOutcomeEnum
    {
        /// <summary>
        /// Authenticated.
        /// </summary>
        Success,

        /// <summary>
        /// Principal or session not found.
        /// </summary>
        NotFound,

        /// <summary>
        /// Principal, tenant, or session inactive or expired.
        /// </summary>
        Inactive,

        /// <summary>
        /// Malformed, conflicting, or incorrect credentials.
        /// </summary>
        Invalid,

        /// <summary>
        /// No credentials were supplied.
        /// </summary>
        NotAttempted
    }
}
