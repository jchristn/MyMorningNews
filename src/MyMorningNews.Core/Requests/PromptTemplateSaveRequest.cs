namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Save a new version of a prompt template.
    /// </summary>
    public class PromptTemplateSaveRequest
    {
        /// <summary>
        /// Prompt Markdown including front matter.
        /// </summary>
        public string Content { get; set; } = String.Empty;

        /// <summary>
        /// Change note.
        /// </summary>
        public string? ChangeNote { get; set; } = null;
    }
}
