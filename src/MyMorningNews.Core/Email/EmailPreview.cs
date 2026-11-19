namespace MyMorningNews.Core.Email
{
    using System;

    /// <summary>
    /// A rendered email that was not sent.
    /// </summary>
    public class EmailPreview
    {
        /// <summary>
        /// Recipient.
        /// </summary>
        public string Recipient { get; set; } = String.Empty;

        /// <summary>
        /// Subject.
        /// </summary>
        public string Subject { get; set; } = String.Empty;

        /// <summary>
        /// HTML.
        /// </summary>
        public string Html { get; set; } = String.Empty;

        /// <summary>
        /// Plain text.
        /// </summary>
        public string Text { get; set; } = String.Empty;
    }
}
