namespace MyMorningNews.Core.Requests
{
    /// <summary>
    /// Save where the caller stopped listening to a briefing.
    /// </summary>
    public class BriefingProgressRequest
    {
        /// <summary>
        /// Playback position in seconds. Minimum 0, maximum 86400.
        /// </summary>
        public double PositionSeconds { get; set; } = 0;

        /// <summary>
        /// Episode length in seconds, when the player knows it.
        /// </summary>
        public double? DurationSeconds { get; set; } = null;

        /// <summary>
        /// Whether the caller finished listening.
        /// </summary>
        public bool Completed { get; set; } = false;
    }
}
