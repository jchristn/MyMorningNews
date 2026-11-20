namespace MyMorningNews.Sdk.Models
{
    using System;

    /// <summary>
    /// Answer to a public account request that always succeeds (HTTP 202), such as resending a verification link or asking for a password reset, so it never reveals whether an account exists.
    /// </summary>
    public class AccountMessageResponse
    {
        /// <summary>
        /// Message to show the person.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AccountMessageResponse()
        {
        }
    }
}
