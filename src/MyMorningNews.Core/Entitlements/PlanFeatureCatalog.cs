namespace MyMorningNews.Core.Entitlements
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Validates plan features and user overrides. Unknown features are refused rather than silently granting access.
    /// Thread safety: safe for concurrent callers.
    /// </summary>
    public static class PlanFeatureCatalog
    {
        private static readonly HashSet<string> _BooleanKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "audio.enabled", "email.enabled", "mobile.access", "endpoints.custom", "customResearch.enabled",
            "oneTimeResearch.enabled", "catalog.audio.enabled", "models.personalConnections.enabled", "catalog.freeOnly"
        };

        private static readonly HashSet<string> _CountKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "topics.max", "customTopics.max", "systemSubscriptions.max", "schedule.minIntervalMinutes", "runs.daily.max",
            "audio.monthlyMinutes.max", "audio.dailySeconds.max", "artifacts.pinned.max"
        };

        /// <summary>
        /// Validate and normalize one feature. Numeric zero disables an allowance; unlimited removes its quantity cap.
        /// </summary>
        /// <param name="key">Registered feature key.</param>
        /// <param name="value">Value to normalize.</param>
        /// <returns>Canonical value.</returns>
        /// <exception cref="ArgumentException">Unknown feature or invalid value.</exception>
        public static string Normalize(string key, string? value)
        {
            if (String.IsNullOrWhiteSpace(key) || String.IsNullOrWhiteSpace(value)) throw new ArgumentException("A feature key and value are required.");
            string raw = value.Trim();
            if (_BooleanKeys.Contains(key))
            {
                if (!Boolean.TryParse(raw, out bool enabled)) throw new ArgumentException(key + " must be true or false.");
                return enabled ? "true" : "false";
            }

            if (_CountKeys.Contains(key) || key == "usage.monthlyCostUsd.max")
            {
                if (String.Equals(raw, "unlimited", StringComparison.OrdinalIgnoreCase)) return "unlimited";
                if (!Decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount)
                    || amount < 0 || amount > 1000000000 || (_CountKeys.Contains(key) && amount != Decimal.Truncate(amount)))
                    throw new ArgumentException(key + " must be a non-negative " + (_CountKeys.Contains(key) ? "whole number" : "number") + " or unlimited.");
                return amount.ToString(CultureInfo.InvariantCulture);
            }

            throw new ArgumentException("Unknown entitlement feature: " + key + ".");
        }
    }
}
