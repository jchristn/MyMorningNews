namespace MyMorningNews.Sdk.Models
{
    using System;

    /// <summary>
    /// Answer to a public sign-up (HTTP 202). The server answers the same way whether or not the email already has an account.
    /// </summary>
    public class SignupResponse
    {
        /// <summary>
        /// Message to show the person.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Whether the account must confirm its email before signing in. Default false.
        /// </summary>
        public bool VerificationRequired { get; set; } = false;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SignupResponse()
        {
        }
    }
}
