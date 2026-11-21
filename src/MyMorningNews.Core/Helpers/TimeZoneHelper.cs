namespace MyMorningNews.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// IANA time zone validation, conversion, and display labels.
    /// Thread safety: stateless; safe for concurrent use.
    /// </summary>
    public static class TimeZoneHelper
    {
        private static readonly Dictionary<string, string[]> _Abbreviations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "UTC", new[] { "UTC", "UTC" } },
            { "Etc/UTC", new[] { "UTC", "UTC" } },
            { "America/New_York", new[] { "EST", "EDT" } },
            { "America/Toronto", new[] { "EST", "EDT" } },
            { "America/Detroit", new[] { "EST", "EDT" } },
            { "America/Chicago", new[] { "CST", "CDT" } },
            { "America/Winnipeg", new[] { "CST", "CDT" } },
            { "America/Mexico_City", new[] { "CST", "CST" } },
            { "America/Denver", new[] { "MST", "MDT" } },
            { "America/Edmonton", new[] { "MST", "MDT" } },
            { "America/Phoenix", new[] { "MST", "MST" } },
            { "America/Los_Angeles", new[] { "PST", "PDT" } },
            { "America/Vancouver", new[] { "PST", "PDT" } },
            { "America/Anchorage", new[] { "AKST", "AKDT" } },
            { "America/Halifax", new[] { "AST", "ADT" } },
            { "Pacific/Honolulu", new[] { "HST", "HST" } },
            { "Europe/London", new[] { "GMT", "BST" } },
            { "Europe/Dublin", new[] { "GMT", "IST" } },
            { "Europe/Lisbon", new[] { "WET", "WEST" } },
            { "Europe/Paris", new[] { "CET", "CEST" } },
            { "Europe/Berlin", new[] { "CET", "CEST" } },
            { "Europe/Madrid", new[] { "CET", "CEST" } },
            { "Europe/Rome", new[] { "CET", "CEST" } },
            { "Europe/Amsterdam", new[] { "CET", "CEST" } },
            { "Europe/Brussels", new[] { "CET", "CEST" } },
            { "Europe/Vienna", new[] { "CET", "CEST" } },
            { "Europe/Zurich", new[] { "CET", "CEST" } },
            { "Europe/Stockholm", new[] { "CET", "CEST" } },
            { "Europe/Oslo", new[] { "CET", "CEST" } },
            { "Europe/Copenhagen", new[] { "CET", "CEST" } },
            { "Europe/Warsaw", new[] { "CET", "CEST" } },
            { "Europe/Prague", new[] { "CET", "CEST" } },
            { "Europe/Athens", new[] { "EET", "EEST" } },
            { "Europe/Helsinki", new[] { "EET", "EEST" } },
            { "Europe/Kiev", new[] { "EET", "EEST" } },
            { "Europe/Kyiv", new[] { "EET", "EEST" } },
            { "Europe/Moscow", new[] { "MSK", "MSK" } },
            { "Asia/Kolkata", new[] { "IST", "IST" } },
            { "Asia/Tokyo", new[] { "JST", "JST" } },
            { "Asia/Seoul", new[] { "KST", "KST" } },
            { "Asia/Shanghai", new[] { "CST", "CST" } },
            { "Asia/Hong_Kong", new[] { "HKT", "HKT" } },
            { "Asia/Singapore", new[] { "SGT", "SGT" } },
            { "Australia/Sydney", new[] { "AEST", "AEDT" } },
            { "Australia/Melbourne", new[] { "AEST", "AEDT" } },
            { "Australia/Brisbane", new[] { "AEST", "AEST" } },
            { "Australia/Perth", new[] { "AWST", "AWST" } },
            { "Pacific/Auckland", new[] { "NZST", "NZDT" } }
        };

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

        /// <summary>
        /// Short label for a zone at an instant: the common abbreviation for well-known zones (PST or PDT, HST, CET or
        /// CEST, JST), otherwise the UTC offset (UTC+5:30). UTC is "UTC".
        /// </summary>
        /// <param name="zone">Zone.</param>
        /// <param name="utc">Instant, in UTC.</param>
        /// <returns>Label.</returns>
        /// <exception cref="ArgumentNullException">zone is null.</exception>
        public static string Abbreviation(TimeZoneInfo zone, DateTime utc)
        {
            ArgumentNullException.ThrowIfNull(zone);
            DateTime instant = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            bool daylight = zone.IsDaylightSavingTime(instant);
            if (_Abbreviations.TryGetValue(zone.Id, out string[]? pair)) return daylight ? pair[1] : pair[0];
            TimeSpan offset = zone.GetUtcOffset(instant);
            if (offset == TimeSpan.Zero && (zone.Id == "UTC" || zone.Id == "Etc/UTC" || zone.Id == "Etc/GMT" || zone.Id == "GMT")) return "UTC";
            string sign = offset < TimeSpan.Zero ? "-" : "+";
            TimeSpan abs = offset.Duration();
            return "UTC" + sign + abs.Hours + (abs.Minutes != 0 ? ":" + abs.Minutes.ToString("00", CultureInfo.InvariantCulture) : String.Empty);
        }
    }
}
