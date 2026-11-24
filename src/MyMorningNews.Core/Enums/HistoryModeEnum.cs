namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// How a briefing compares with its earlier issues. Off supplies no prior briefing text.
    /// </summary>
    public enum HistoryModeEnum
    {
        /// <summary>Produce a standalone briefing.</summary>
        Off,
        /// <summary>Compare with the latest ready issue.</summary>
        PreviousIssue,
        /// <summary>Compare with a bounded number of recent ready issues.</summary>
        RecentIssues,
        /// <summary>Compare with ready issues since a selected date, subject to the count and token bounds.</summary>
        SinceDate,
        /// <summary>Compare with one explicitly selected issue of this topic.</summary>
        SpecificIssue
    }
}
