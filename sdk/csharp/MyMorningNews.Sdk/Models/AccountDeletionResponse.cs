namespace MyMorningNews.Sdk.Models
{
    using System;

    /// <summary>
    /// Answer to an account deletion request (HTTP 202). Signing in again before PurgeAfterUtc cancels the deletion.
    /// </summary>
    public class AccountDeletionResponse
    {
        /// <summary>
        /// When the account and its data are purged (UTC).
        /// </summary>
        public DateTime PurgeAfterUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Message to show the person.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AccountDeletionResponse()
        {
        }
    }
}
