namespace MyMorningNews.Core.Prompts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Rendered system and user prompt text.
    /// </summary>
    public class RenderedPrompt
    {
        /// <summary>
        /// System prompt.
        /// </summary>
        public string System { get; set; } = String.Empty;

        /// <summary>
        /// User prompt.
        /// </summary>
        public string User { get; set; } = String.Empty;

        /// <summary>
        /// Variables referenced but not declared.
        /// </summary>
        public List<string> UnknownVariables { get; set; } = new List<string>();
    }
}
