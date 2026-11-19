namespace MyMorningNews.Core.Audio
{
    using System;

    /// <summary>
    /// A chapter in the chapters JSON (Podcasting 2.0 shape).
    /// </summary>
    public class ChapterEntry
    {
        /// <summary>
        /// Start in seconds.
        /// </summary>
        public double StartTime { get; set; } = 0;

        /// <summary>
        /// End in seconds.
        /// </summary>
        public double EndTime { get; set; } = 0;

        /// <summary>
        /// Title.
        /// </summary>
        public string Title { get; set; } = String.Empty;
    }
}
