namespace MyMorningNews.Core.Email
{
    using System;

    /// <summary>
    /// Outcome of one send.
    /// </summary>
    public class EmailSendResult
    {
        /// <summary>
        /// Sent.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Provider message ID (angle brackets removed).
        /// </summary>
        public string? ProviderMessageId { get; set; } = null;

        /// <summary>
        /// Error, when not sent.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// The failure will not succeed on retry (bad address, bad configuration).
        /// </summary>
        public bool Permanent { get; set; } = false;
    }
}
