namespace MyMorningNews.Core.Email
{
    using System;

    /// <summary>
    /// A labeled link in an email.
    /// </summary>
    public class EmailLink
    {
        /// <summary>
        /// Label.
        /// </summary>
        public string Label { get; set; } = String.Empty;

        /// <summary>
        /// URL.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EmailLink()
        {
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="label">Label.</param>
        /// <param name="url">URL.</param>
        public EmailLink(string label, string url)
        {
            Label = label ?? String.Empty;
            Url = url ?? String.Empty;
        }
    }
}
