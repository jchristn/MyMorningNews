namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Request capture and history settings.
    /// </summary>
    public class RequestHistorySettings
    {
        #region Public-Members

        /// <summary>
        /// Whether request capture is enabled. Default true.
        /// </summary>
        [Setting(Description = "Capture every HTTP request.")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum request body bytes captured. Default 65536. Minimum 0, maximum 1048576.
        /// </summary>
        [Setting(Description = "Maximum request body bytes captured.", Minimum = 0, Maximum = 1048576)]
        public int MaxRequestBodyBytes
        {
            get
            {
                return _MaxRequestBodyBytes;
            }
            set
            {
                _MaxRequestBodyBytes = Math.Clamp(value, 0, 1024 * 1024);
            }
        }

        /// <summary>
        /// Maximum response body bytes captured. Default 65536. Minimum 0, maximum 1048576.
        /// </summary>
        [Setting(Description = "Maximum response body bytes captured.", Minimum = 0, Maximum = 1048576)]
        public int MaxResponseBodyBytes
        {
            get
            {
                return _MaxResponseBodyBytes;
            }
            set
            {
                _MaxResponseBodyBytes = Math.Clamp(value, 0, 1024 * 1024);
            }
        }

        /// <summary>
        /// Retention in days. Default 30. Minimum 1, maximum 3650.
        /// </summary>
        [Setting(Description = "Days to keep captured requests.", Minimum = 1, Maximum = 3650)]
        public int RetentionDays
        {
            get
            {
                return _RetentionDays;
            }
            set
            {
                _RetentionDays = Math.Clamp(value, 1, 3650);
            }
        }

        #endregion

        #region Private-Members

        private int _MaxRequestBodyBytes = 65536;
        private int _MaxResponseBodyBytes = 65536;
        private int _RetentionDays = 30;

        #endregion
    }
}
