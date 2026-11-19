namespace MyMorningNews.Core.Responses
{
    using System.Collections.Generic;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Current settings (secrets masked) and per-field metadata.
    /// </summary>
    public class SettingsResponse
    {
        /// <summary>
        /// Settings as currently in effect, with secret values replaced by the mask.
        /// </summary>
        public ServerSettings Settings { get; set; } = new ServerSettings();

        /// <summary>
        /// Field metadata.
        /// </summary>
        public List<SettingFieldDescriptor> Fields { get; set; } = new List<SettingFieldDescriptor>();

        /// <summary>
        /// Paths saved to the file that take effect after a restart.
        /// </summary>
        public List<string> PendingRestart { get; set; } = new List<string>();
    }
}
