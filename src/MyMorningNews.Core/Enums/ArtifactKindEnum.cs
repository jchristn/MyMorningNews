namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// What a stored file is.
    /// </summary>
    public enum ArtifactKindEnum
    {
        /// <summary>
        /// Briefing Markdown source.
        /// </summary>
        BriefingMarkdown,

        /// <summary>
        /// Rendered, self-contained HTML report.
        /// </summary>
        BriefingHtml,

        /// <summary>
        /// Audio episode.
        /// </summary>
        Audio,

        /// <summary>
        /// Audio chapters JSON.
        /// </summary>
        Chapters,

        /// <summary>
        /// Account export.
        /// </summary>
        Export
    }
}
