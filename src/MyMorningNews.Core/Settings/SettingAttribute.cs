namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Describes a setting for the dashboard: whether a change applies live or needs a restart, whether it is secret,
    /// and which environment variable overrides it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class SettingAttribute : Attribute
    {
        /// <summary>
        /// Human-readable description.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Whether a change takes effect only after a restart. Default false (applied live).
        /// </summary>
        public bool RestartRequired { get; set; } = false;

        /// <summary>
        /// Whether the value is secret (masked when read; only written when a new value is supplied).
        /// </summary>
        public bool Secret { get; set; } = false;

        /// <summary>
        /// Environment variable name (without prefix) that overrides this setting, or empty when none.
        /// </summary>
        public string EnvironmentVariable { get; set; } = String.Empty;

        /// <summary>
        /// Minimum value for numeric settings, or null.
        /// </summary>
        public double Minimum { get; set; } = Double.NaN;

        /// <summary>
        /// Maximum value for numeric settings, or null.
        /// </summary>
        public double Maximum { get; set; } = Double.NaN;
    }
}
