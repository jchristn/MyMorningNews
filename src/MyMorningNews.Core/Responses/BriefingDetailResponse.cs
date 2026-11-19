namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A briefing with its sections, items, and stored files.
    /// </summary>
    public class BriefingDetailResponse
    {
        /// <summary>
        /// Briefing.
        /// </summary>
        public Briefing Briefing { get; set; } = new Briefing();

        /// <summary>
        /// Sections in order.
        /// </summary>
        public List<BriefingSection> Sections { get; set; } = new List<BriefingSection>();

        /// <summary>
        /// Items covered.
        /// </summary>
        public List<BriefingItem> Items { get; set; } = new List<BriefingItem>();

        /// <summary>
        /// Stored files.
        /// </summary>
        public List<Artifact> Files { get; set; } = new List<Artifact>();

        /// <summary>
        /// Whether the Markdown or HTML file expired (regenerate to restore).
        /// </summary>
        public bool FilesExpired { get; set; } = false;

        /// <summary>
        /// Edition name.
        /// </summary>
        public string? EditionName { get; set; } = null;

        /// <summary>
        /// Topic name.
        /// </summary>
        public string? TopicName { get; set; } = null;

        /// <summary>
        /// Display titles of the items by item ID (finding title, prediction statement, or suggestion title).
        /// </summary>
        public Dictionary<string, string> ItemTitles { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
