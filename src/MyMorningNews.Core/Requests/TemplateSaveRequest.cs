namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Save a new template version.
    /// </summary>
    public class TemplateSaveRequest
    {
        /// <summary>
        /// Template Markdown.
        /// </summary>
        public string Content { get; set; } = String.Empty;

        /// <summary>
        /// What changed.
        /// </summary>
        public string? ChangeNote { get; set; } = null;
    }
}
