namespace MyMorningNews.Sdk.Options
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Formats query string values the way the server parses them: ISO 8601 UTC dates, lowercase booleans, invariant numbers,
    /// and enum member names.
    /// </summary>
    public static class QueryFormat
    {
        /// <summary>
        /// Add a value to a query when it is not null (and, for strings, not empty).
        /// </summary>
        /// <param name="query">Query to add to.</param>
        /// <param name="key">Key.</param>
        /// <param name="value">Value, or null to skip.</param>
        /// <exception cref="ArgumentNullException">query or key is null.</exception>
        public static void Add(IDictionary<string, string> query, string key, object? value)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(key);
            string? text = Format(value);
            if (text != null) query[key] = text;
        }

        /// <summary>
        /// Format one value.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Text, or null when the value is null or an empty string.</returns>
        public static string? Format(object? value)
        {
            switch (value)
            {
                case null: return null;
                case string s: return String.IsNullOrWhiteSpace(s) ? null : s.Trim();
                case bool b: return b ? "true" : "false";
                case DateTime d: return d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
                case DateTimeOffset o: return o.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
                case Enum e: return e.ToString();
                case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }
    }
}
