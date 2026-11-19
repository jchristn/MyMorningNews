namespace MyMorningNews.Core.Audio
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A script part that becomes a chapter.
    /// </summary>
    public class ScriptPart
    {
        /// <summary>
        /// Chapter title.
        /// </summary>
        public string Heading { get; set; } = "Introduction";

        /// <summary>
        /// Lines.
        /// </summary>
        public List<ScriptLine> Lines { get; set; } = new List<ScriptLine>();
    }
}
