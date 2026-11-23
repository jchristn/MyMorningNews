namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Everything the public pages of one share show: the briefing, its topic or edition name, the audio when it is
    /// ready, and the sharing user's display name when they chose to show it. Never carries an email address.
    /// </summary>
    public class SharedBriefing
    {
        /// <summary>
        /// The share.
        /// </summary>
        public BriefingShare Share { get; set; } = new BriefingShare();

        /// <summary>
        /// The briefing.
        /// </summary>
        public Briefing Briefing { get; set; } = new Briefing();

        /// <summary>
        /// Topic name, or the edition name for an edition briefing. May be null.
        /// </summary>
        public string? TopicName { get; set; } = null;

        /// <summary>
        /// Display name of the sharing user, only when the share shows it and the user has a name. May be null.
        /// </summary>
        public string? OwnerName { get; set; } = null;

        /// <summary>
        /// Finished audio for the briefing. Null when there is none yet.
        /// </summary>
        public MediaAsset? Audio { get; set; } = null;

        /// <summary>
        /// The stored audio file. Null when there is no finished audio.
        /// </summary>
        public Artifact? AudioFile { get; set; } = null;

        /// <summary>
        /// Chapters of the audio in order. Empty when there are none.
        /// </summary>
        public List<MediaChapter> Chapters
        {
            get { return _Chapters; }
            set { _Chapters = value ?? new List<MediaChapter>(); }
        }

        private List<MediaChapter> _Chapters = new List<MediaChapter>();
    }
}
