namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Pin or unpin a briefing.
    /// </summary>
    public class BriefingPinRequest
    {
        /// <summary>
        /// Pinned.
        /// </summary>
        public bool Pinned { get; set; } = true;
    }
}
