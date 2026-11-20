namespace MyMorningNews.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// Typed, validated access to tool or prompt arguments. Problems throw <see cref="McpToolException"/>, whose message
    /// the calling model sees, so it can correct the call.
    /// </summary>
    public class ToolArguments
    {
        #region Private-Members

        private readonly RpcParameters? _Parameters;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Wrap arguments.
        /// </summary>
        /// <param name="parameters">Arguments; null means none were sent.</param>
        public ToolArguments(RpcParameters? parameters)
        {
            _Parameters = parameters;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// A trimmed string argument, or null when missing or blank.
        /// </summary>
        /// <param name="name">Argument name.</param>
        /// <returns>Value or null.</returns>
        public string? Text(string name)
        {
            string? value = _Parameters?.GetString(name);
            return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// A required string argument.
        /// </summary>
        /// <param name="name">Argument name.</param>
        /// <param name="maxLength">Maximum length after trimming. Default 4000.</param>
        /// <returns>Value.</returns>
        /// <exception cref="McpToolException">Missing, blank, or too long.</exception>
        public string RequiredText(string name, int maxLength = 4000)
        {
            string? value = Text(name);
            if (value == null) throw new McpToolException(name + " is required.");
            if (value.Length > maxLength) throw new McpToolException(name + " must be at most " + maxLength + " characters.");
            return value;
        }

        /// <summary>
        /// An optional string argument limited in length.
        /// </summary>
        /// <param name="name">Argument name.</param>
        /// <param name="maxLength">Maximum length after trimming.</param>
        /// <returns>Value or null.</returns>
        /// <exception cref="McpToolException">Too long.</exception>
        public string? OptionalText(string name, int maxLength)
        {
            string? value = Text(name);
            if (value != null && value.Length > maxLength) throw new McpToolException(name + " must be at most " + maxLength + " characters.");
            return value;
        }

        /// <summary>
        /// An integer argument clamped to a range.
        /// </summary>
        /// <param name="name">Argument name.</param>
        /// <param name="defaultValue">Value when missing.</param>
        /// <param name="minimum">Minimum.</param>
        /// <param name="maximum">Maximum.</param>
        /// <returns>Value.</returns>
        public int Integer(string name, int defaultValue, int minimum, int maximum)
        {
            long? value = _Parameters?.GetInt64(name);
            if (!value.HasValue) return Math.Clamp(defaultValue, minimum, maximum);
            return (int)Math.Clamp(value.Value, minimum, maximum);
        }

        /// <summary>
        /// A number argument, or null when missing.
        /// </summary>
        /// <param name="name">Argument name.</param>
        /// <param name="minimum">Minimum.</param>
        /// <param name="maximum">Maximum.</param>
        /// <returns>Value or null.</returns>
        /// <exception cref="McpToolException">Outside the range.</exception>
        public double? Number(string name, double minimum, double maximum)
        {
            double? value = _Parameters?.GetDouble(name);
            if (!value.HasValue) return null;
            if (value.Value < minimum || value.Value > maximum) throw new McpToolException(name + " must be between " + minimum.ToString(CultureInfo.InvariantCulture) + " and " + maximum.ToString(CultureInfo.InvariantCulture) + ".");
            return value.Value;
        }

        /// <summary>
        /// A boolean argument, or null when missing.
        /// </summary>
        /// <param name="name">Argument name.</param>
        /// <returns>Value or null.</returns>
        public bool? Flag(string name)
        {
            return _Parameters?.GetBoolean(name);
        }

        /// <summary>
        /// An ISO 8601 date and time argument, or null when missing. Values without a zone are treated as UTC.
        /// </summary>
        /// <param name="name">Argument name.</param>
        /// <returns>UTC value or null.</returns>
        /// <exception cref="McpToolException">Not a date.</exception>
        public DateTime? Date(string name)
        {
            string? value = Text(name);
            if (value == null) return null;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
                throw new McpToolException(name + " must be an ISO 8601 date, for example 2026-11-19 or 2026-11-19T08:00:00Z.");
            return parsed;
        }

        /// <summary>
        /// An enum argument matched by name, ignoring case and hyphens. Null when missing.
        /// </summary>
        /// <typeparam name="T">Enum type.</typeparam>
        /// <param name="name">Argument name.</param>
        /// <returns>Value or null.</returns>
        /// <exception cref="McpToolException">Not one of the names.</exception>
        public T? Choice<T>(string name) where T : struct, Enum
        {
            string? value = Text(name);
            if (value == null) return null;
            string compact = value.Replace("-", String.Empty).Replace("_", String.Empty).Replace(" ", String.Empty);
            if (Enum.TryParse<T>(compact, true, out T parsed) && Enum.IsDefined(parsed)) return parsed;
            throw new McpToolException(name + " must be one of: " + String.Join(", ", Enum.GetNames<T>()) + ".");
        }

        /// <summary>
        /// A list of strings, or an empty list when missing.
        /// </summary>
        /// <param name="name">Argument name.</param>
        /// <param name="maxItems">Maximum items. Default 50.</param>
        /// <returns>Values, trimmed, blanks removed.</returns>
        /// <exception cref="McpToolException">Not an array of strings or too many items.</exception>
        public List<string> TextList(string name, int maxItems = 50)
        {
            List<string> list = new List<string>();
            if (_Parameters == null || !_Parameters.HasValue || !_Parameters.ContainsProperty(name)) return list;
            using (JsonDocument doc = JsonDocument.Parse(_Parameters.RawJson!))
            {
                if (!doc.RootElement.TryGetProperty(name, out JsonElement element) || element.ValueKind == JsonValueKind.Null) return list;
                if (element.ValueKind != JsonValueKind.Array) throw new McpToolException(name + " must be an array of strings.");
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) throw new McpToolException(name + " must be an array of strings.");
                    string? s = item.GetString();
                    if (!String.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
                }
            }

            if (list.Count > maxItems) throw new McpToolException(name + " can hold at most " + maxItems + " items.");
            return list;
        }

        #endregion
    }
}
