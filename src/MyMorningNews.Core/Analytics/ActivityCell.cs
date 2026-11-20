namespace MyMorningNews.Core.Analytics
{
    /// <summary>
    /// Runs created at one hour of one day of the week, in the report's time zone.
    /// </summary>
    public class ActivityCell
    {
        /// <summary>
        /// Day of week, 0 (Sunday) to 6 (Saturday).
        /// </summary>
        public int DayOfWeek { get; set; } = 0;

        /// <summary>
        /// Hour, 0 to 23.
        /// </summary>
        public int Hour { get; set; } = 0;

        /// <summary>
        /// Runs.
        /// </summary>
        public int Count { get; set; } = 0;
    }
}
