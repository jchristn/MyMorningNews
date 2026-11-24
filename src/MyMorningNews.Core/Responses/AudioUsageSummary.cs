namespace MyMorningNews.Core.Responses
{
    /// <summary>Exact daily audio accounting. Generated milliseconds remain after an episode is deleted; reservations cover unfinished jobs.</summary>
    public class AudioUsageSummary
    {
        /// <summary>Successfully generated milliseconds, charged once per logical operation.</summary>
        public long GeneratedMilliseconds { get; set; } = 0;

        /// <summary>Seconds held for queued or running jobs.</summary>
        public long ReservedSeconds { get; set; } = 0;
    }
}
