namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Diagnostic capture settings.
    /// </summary>
    public class DiagnosticsSettings
    {
        #region Public-Members

        /// <summary>
        /// Store rendered prompts and raw responses on model call logs. Default true.
        /// </summary>
        [Setting(Description = "Store prompts and responses on model call logs.")]
        public bool CaptureLlmPayloads { get; set; } = true;

        /// <summary>
        /// Days to keep model call payloads before they are cleared. Default 14. Minimum 1, maximum 365.
        /// </summary>
        [Setting(Description = "Days to keep model call payloads.", Minimum = 1, Maximum = 365)]
        public int LlmPayloadRetentionDays
        {
            get { return _LlmPayloadRetentionDays; }
            set { _LlmPayloadRetentionDays = Math.Clamp(value, 1, 365); }
        }

        /// <summary>
        /// Days to keep audit records, or 0 to keep them forever. Default 0. Minimum 0, maximum 3650.
        /// </summary>
        [Setting(Description = "Days to keep audit records (0 keeps them forever).", Minimum = 0, Maximum = 3650)]
        public int AuditRetentionDays
        {
            get { return _AuditRetentionDays; }
            set { _AuditRetentionDays = Math.Clamp(value, 0, 3650); }
        }

        /// <summary>
        /// Maximum characters of prompt or response stored per call. Default 200000. Minimum 1000, maximum 5000000.
        /// </summary>
        [Setting(Description = "Maximum characters stored per prompt or response.", Minimum = 1000, Maximum = 5000000)]
        public int MaxPayloadCharacters
        {
            get { return _MaxPayloadCharacters; }
            set { _MaxPayloadCharacters = Math.Clamp(value, 1000, 5000000); }
        }

        #endregion

        #region Private-Members

        private int _AuditRetentionDays = 0;
        private int _LlmPayloadRetentionDays = 14;
        private int _MaxPayloadCharacters = 200000;

        #endregion
    }
}
