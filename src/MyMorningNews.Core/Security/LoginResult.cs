namespace MyMorningNews.Core.Security
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Result of a login or refresh.
    /// </summary>
    public class LoginResult
    {
        /// <summary>
        /// Whether a token was issued.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// HTTP status to return: 200, 400, 401, or 429.
        /// </summary>
        public int StatusCode { get; set; } = 401;

        /// <summary>
        /// Outcome.
        /// </summary>
        public AuthenticationOutcomeEnum Outcome { get; set; } = AuthenticationOutcomeEnum.Invalid;

        /// <summary>
        /// Message safe to return.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Opaque session token.
        /// </summary>
        public string? Token { get; set; } = null;

        /// <summary>
        /// Session record.
        /// </summary>
        public AuthSession? Session { get; set; } = null;

        /// <summary>
        /// User principal or owner.
        /// </summary>
        public User? User { get; set; } = null;

        /// <summary>
        /// Credential principal.
        /// </summary>
        public Credential? Credential { get; set; } = null;

        /// <summary>
        /// Tenant.
        /// </summary>
        public Tenant? Tenant { get; set; } = null;

        /// <summary>
        /// Create a failure.
        /// </summary>
        /// <param name="statusCode">Status code.</param>
        /// <param name="outcome">Outcome.</param>
        /// <param name="message">Message.</param>
        /// <returns>Result.</returns>
        public static LoginResult Fail(int statusCode, AuthenticationOutcomeEnum outcome, string message)
        {
            return new LoginResult { Success = false, StatusCode = statusCode, Outcome = outcome, Message = message ?? String.Empty };
        }
    }
}
