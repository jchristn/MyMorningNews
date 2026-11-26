namespace MyMorningNews.Core.Briefings
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;

    /// <summary>
    /// Comparison settings shared by topics and editions, with optional unsaved overrides for a preview.
    /// </summary>
    public class HistoryPolicy
    {
        /// <summary>
        /// Comparison mode. Default Off.
        /// </summary>
        public HistoryModeEnum Mode { get; set; } = HistoryModeEnum.Off;

        /// <summary>
        /// Maximum baseline issues for RecentIssues and SinceDate. Default 5; valid 1 to 20.
        /// </summary>
        public int Count { get; set; } = 5;

        /// <summary>
        /// Earliest baseline time for SinceDate, otherwise null.
        /// </summary>
        public DateTime? SinceUtc { get; set; } = null;

        /// <summary>
        /// Pinned baseline for SpecificIssue, otherwise null.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Comparison budget in UTF-8 bytes. Default 8000; valid 256 to 32000.
        /// </summary>
        public int TokenBudget { get; set; } = 8000;

        /// <summary>
        /// The saved settings of a topic.
        /// </summary>
        /// <param name="topic">Topic.</param>
        /// <returns>Policy.</returns>
        /// <exception cref="ArgumentNullException">topic is null.</exception>
        public static HistoryPolicy From(Topic topic)
        {
            ArgumentNullException.ThrowIfNull(topic);
            return new HistoryPolicy { Mode = topic.HistoryMode, Count = topic.HistoryCount, SinceUtc = topic.HistorySinceUtc, BriefingId = topic.HistoryBriefingId, TokenBudget = topic.HistoryTokenBudget };
        }

        /// <summary>
        /// The saved settings of an edition.
        /// </summary>
        /// <param name="edition">Edition.</param>
        /// <returns>Policy.</returns>
        /// <exception cref="ArgumentNullException">edition is null.</exception>
        public static HistoryPolicy From(Edition edition)
        {
            ArgumentNullException.ThrowIfNull(edition);
            return new HistoryPolicy { Mode = edition.HistoryMode, Count = edition.HistoryCount, SinceUtc = edition.HistorySinceUtc, BriefingId = edition.HistoryBriefingId, TokenBudget = edition.HistoryTokenBudget };
        }

        /// <summary>
        /// A copy with any non-null preview values applied. An empty historyBriefingId clears the pin.
        /// </summary>
        /// <param name="request">Unsaved values, or null for none.</param>
        /// <returns>New policy.</returns>
        public HistoryPolicy With(HistoryPreviewRequest? request)
        {
            HistoryPolicy copy = new HistoryPolicy { Mode = Mode, Count = Count, SinceUtc = SinceUtc, BriefingId = BriefingId, TokenBudget = TokenBudget };
            if (request == null) return copy;
            if (request.HistoryMode.HasValue) copy.Mode = request.HistoryMode.Value;
            if (request.HistoryCount.HasValue) copy.Count = request.HistoryCount.Value;
            if (request.HistorySinceUtc.HasValue) copy.SinceUtc = request.HistorySinceUtc;
            if (request.HistoryBriefingId != null) copy.BriefingId = request.HistoryBriefingId.Length == 0 ? null : request.HistoryBriefingId;
            if (request.HistoryTokenBudget.HasValue) copy.TokenBudget = request.HistoryTokenBudget.Value;
            return copy;
        }
    }
}
