namespace MyMorningNews.Core.Helpers
{
    using System;

    /// <summary>
    /// IANA time zone validation and conversion.
    /// </summary>
    public static class TimeZoneHelper
    {
        /// <summary>
        /// Validate a time zone name.
        /// </summary>
        /// <param name="tz">IANA name; null or empty is valid (unset).</param>
        /// <returns>Error message, or null when valid.</returns>
        public static string? Validate(string? tz)
        {
            if (String.IsNullOrWhiteSpace(tz)) return null;
            return TryFind(tz) != null ? null : "Unknown time zone '" + tz + "'. Use an IANA name such as America/Los_Angeles.";
        }

        /// <summary>
        /// Find a time zone by IANA name.
        /// </summary>
        /// <param name="tz">IANA name.</param>
        /// <returns>Time zone, or null when unknown.</returns>
        public static TimeZoneInfo? TryFind(string? tz)
        {
            if (String.IsNullOrWhiteSpace(tz)) return null;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(tz.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
                return null;
            }
            catch (InvalidTimeZoneException)
            {
                return null;
            }
        }
    }
}
