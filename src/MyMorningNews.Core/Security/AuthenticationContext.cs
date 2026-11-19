namespace MyMorningNews.Core.Security
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Result of authenticating a request.
    /// </summary>
    public class AuthenticationContext
    {
        /// <summary>
        /// Outcome.
        /// </summary>
        public AuthenticationOutcomeEnum Outcome { get; set; } = AuthenticationOutcomeEnum.NotAttempted;

        /// <summary>
        /// Scheme used, when one was presented.
        /// </summary>
        public AuthSchemeEnum? Scheme { get; set; } = null;

        /// <summary>
        /// Principal type.
        /// </summary>
        public PrincipalTypeEnum? PrincipalType { get; set; } = null;

        /// <summary>
        /// Principal identifier (user or credential ID, or "system" for the admin key).
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Session identifier, when a session token was used.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Authenticated tenant.
        /// </summary>
        public Tenant? Tenant { get; set; } = null;

        /// <summary>
        /// Authenticated user (also set for credential principals: the owning user).
        /// </summary>
        public User? User { get; set; } = null;

        /// <summary>
        /// Credential, for credential principals.
        /// </summary>
        public Credential? Credential { get; set; } = null;

        /// <summary>
        /// Whether the system administration key was used.
        /// </summary>
        public bool IsSystemAdministrator { get; set; } = false;

        /// <summary>
        /// Failure description safe to return to the caller.
        /// </summary>
        public string? Message { get; set; } = null;

        /// <summary>
        /// Whether authentication succeeded.
        /// </summary>
        public bool IsAuthenticated
        {
            get { return Outcome == AuthenticationOutcomeEnum.Success; }
        }

        /// <summary>
        /// Create a failure result.
        /// </summary>
        /// <param name="outcome">Outcome.</param>
        /// <param name="message">Message.</param>
        /// <param name="scheme">Scheme, when known.</param>
        /// <returns>Context.</returns>
        public static AuthenticationContext Fail(AuthenticationOutcomeEnum outcome, string message, AuthSchemeEnum? scheme = null)
        {
            return new AuthenticationContext { Outcome = outcome, Message = message ?? String.Empty, Scheme = scheme };
        }
    }
}
