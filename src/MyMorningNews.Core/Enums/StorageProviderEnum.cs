namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Where generated artifacts (briefing Markdown and HTML, audio, exports) are stored.
    /// </summary>
    public enum StorageProviderEnum
    {
        /// <summary>
        /// Local filesystem (the Docker media volume).
        /// </summary>
        Local,

        /// <summary>
        /// Amazon S3 or an S3-compatible service (MinIO, Cloudflare R2, Backblaze B2, and similar).
        /// </summary>
        S3
    }
}
