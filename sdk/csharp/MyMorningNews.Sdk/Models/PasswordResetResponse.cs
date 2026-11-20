namespace MyMorningNews.Sdk.Models
{
    using System;

    /// <summary>
    /// Answer to a successful password reset. Every session of the account is signed out.
    /// </summary>
    public class PasswordResetResponse
    {
        /// <summary>
        /// Whether the password was changed. Default false.
        /// </summary>
        public bool Reset { get; set; } = false;

        /// <summary>
        /// The account's email address.
        /// </summary>
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PasswordResetResponse()
        {
        }
    }
}
