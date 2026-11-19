namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Authorization outcome.
    /// </summary>
    public enum AuthorizationOutcomeEnum
    {
        /// <summary>
        /// Allowed.
        /// </summary>
        Permitted,

        /// <summary>
        /// A Deny permission matched.
        /// </summary>
        DeniedExplicit,

        /// <summary>
        /// No Permit permission matched.
        /// </summary>
        DeniedImplicit,

        /// <summary>
        /// The target resource does not exist in the tenant.
        /// </summary>
        NotFound,

        /// <summary>
        /// Conflicting tenant or resource hints.
        /// </summary>
        Conflict
    }
}
