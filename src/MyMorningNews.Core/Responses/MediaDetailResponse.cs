namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// An audio episode with chapters and download paths.
    /// </summary>
    public class MediaDetailResponse
    {
        /// <summary>
        /// Episode.
        /// </summary>
        public MediaAsset Media { get; set; } = new MediaAsset();

        /// <summary>
        /// Chapters.
        /// </summary>
        public List<MediaChapter> Chapters { get; set; } = new List<MediaChapter>();

        /// <summary>
        /// Authenticated audio path (Range supported).
        /// </summary>
        public string? AudioPath { get; set; } = null;

        /// <summary>
        /// Authenticated chapters JSON path.
        /// </summary>
        public string? ChaptersPath { get; set; } = null;
    }
}
