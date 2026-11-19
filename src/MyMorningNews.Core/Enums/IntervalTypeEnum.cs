namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Schedule interval type.
    /// </summary>
    public enum IntervalTypeEnum
    {
        /// <summary>
        /// Every n minutes.
        /// </summary>
        EveryNMinutes,

        /// <summary>
        /// Every n hours.
        /// </summary>
        EveryNHours,

        /// <summary>
        /// Every n days at times of day.
        /// </summary>
        EveryNDays,

        /// <summary>
        /// Every n weeks on days of week at times of day.
        /// </summary>
        EveryNWeeks,

        /// <summary>
        /// On days of week at times of day.
        /// </summary>
        DaysOfWeek,

        /// <summary>
        /// On days of month at times of day.
        /// </summary>
        DaysOfMonth,

        /// <summary>
        /// Once at a time.
        /// </summary>
        Once,

        /// <summary>
        /// Cron expression.
        /// </summary>
        Cron,

        /// <summary>
        /// Only when run manually.
        /// </summary>
        Manual
    }
}
