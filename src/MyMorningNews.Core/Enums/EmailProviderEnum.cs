namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Email provider.
    /// </summary>
    public enum EmailProviderEnum
    {
        /// <summary>
        /// Mailgun.
        /// </summary>
        Mailgun,

        /// <summary>
        /// Write HTML and text files (default until Mailgun is configured).
        /// </summary>
        File,

        /// <summary>
        /// Do not send.
        /// </summary>
        Disabled
    }
}
