namespace MyMorningNews.Sdk.Models
{
    using System;

    /// <summary>
    /// Answer to a successful email verification.
    /// </summary>
    public class EmailVerifiedResponse
    {
        /// <summary>
        /// Whether the address is now confirmed. Default false.
        /// </summary>
        public bool Verified { get; set; } = false;

        /// <summary>
        /// The confirmed address.
        /// </summary>
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EmailVerifiedResponse()
        {
        }
    }
}
