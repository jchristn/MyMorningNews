namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Metadata for one setting, used by the dashboard Settings page.
    /// </summary>
    public class SettingFieldDescriptor
    {
        /// <summary>
        /// Dotted camelCase path, for example rest.port.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// Group name, for example rest.
        /// </summary>
        public string Group { get; set; } = String.Empty;

        /// <summary>
        /// Field name, for example port.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Value type: string, integer, number, boolean, or enum.
        /// </summary>
        public string Type { get; set; } = "string";

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Whether a change needs a restart.
        /// </summary>
        public bool RestartRequired { get; set; } = false;

        /// <summary>
        /// Whether the value is secret.
        /// </summary>
        public bool Secret { get; set; } = false;

        /// <summary>
        /// For secrets: whether a value is set.
        /// </summary>
        public bool IsSet { get; set; } = false;

        /// <summary>
        /// Whether an environment variable overrides the file value (the field is read-only in the dashboard).
        /// </summary>
        public bool OverriddenByEnvironment { get; set; } = false;

        /// <summary>
        /// Environment variable name that can override this setting, or null.
        /// </summary>
        public string? EnvironmentVariable { get; set; } = null;

        /// <summary>
        /// Minimum for numeric settings.
        /// </summary>
        public double? Minimum { get; set; } = null;

        /// <summary>
        /// Maximum for numeric settings.
        /// </summary>
        public double? Maximum { get; set; } = null;

        /// <summary>
        /// Allowed values for enum settings.
        /// </summary>
        public List<string>? EnumValues { get; set; } = null;
    }
}
