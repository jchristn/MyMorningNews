namespace MyMorningNews.Core.Email
{
    using System;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A newly issued link token: the stored row and the secret that goes in the URL.
    /// </summary>
    public class IssuedToken
    {
        /// <summary>
        /// Stored token (hash only).
        /// </summary>
        public EmailActionToken Token { get; set; } = new EmailActionToken();

        /// <summary>
        /// Secret for the URL.
        /// </summary>
        public string Secret { get; set; } = String.Empty;
    }
}
