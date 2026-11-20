namespace MyMorningNews.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Sign-in was refused because the account's email address is not confirmed yet (HTTP 403 EmailNotVerified). Open the
    /// emailed link (Account.VerifyEmailAsync) or ask for a new one (Account.ResendVerificationAsync), then sign in again.
    /// </summary>
    public class EmailNotVerifiedException : MyMorningNewsException
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="requestId">Request ID, or null.</param>
        /// <param name="details">Details, or null.</param>
        /// <param name="responseBody">Raw body, or null.</param>
        public EmailNotVerifiedException(string message, string? requestId, IReadOnlyDictionary<string, string>? details, string? responseBody)
            : base(403, "EmailNotVerified", message, requestId, details, responseBody)
        {
        }
    }
}
