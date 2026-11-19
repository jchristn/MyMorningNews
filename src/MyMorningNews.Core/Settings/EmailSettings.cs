namespace MyMorningNews.Core.Settings
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Email delivery (plan section 23.6). Link base URLs come from Rest.PublicBaseUrl (feedback pages) and Rest.AppBaseUrl
    /// (detail links).
    /// </summary>
    public class EmailSettings
    {
        #region Public-Members

        /// <summary>
        /// Send email. Default true.
        /// </summary>
        [Setting(Description = "Send email.", EnvironmentVariable = "EMAIL_ENABLED")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Provider. Default File (written to FileDirectory) until Mailgun is configured.
        /// </summary>
        [Setting(Description = "Email provider: Mailgun, File, or Disabled.", EnvironmentVariable = "EMAIL_PROVIDER")]
        public EmailProviderEnum Provider { get; set; } = EmailProviderEnum.File;

        /// <summary>
        /// From address. Default briefings@example.test.
        /// </summary>
        [Setting(Description = "From address.", EnvironmentVariable = "EMAIL_FROM_ADDRESS")]
        public string FromAddress { get; set; } = "briefings@example.test";

        /// <summary>
        /// From display name. Default My Morning News.
        /// </summary>
        [Setting(Description = "From display name.")]
        public string FromName { get; set; } = "My Morning News";

        /// <summary>
        /// Reply-to address, or empty for none.
        /// </summary>
        [Setting(Description = "Reply-to address.")]
        public string ReplyToAddress { get; set; } = String.Empty;

        /// <summary>
        /// Subject prefix. Default "[My Morning News]".
        /// </summary>
        [Setting(Description = "Subject prefix.")]
        public string SubjectPrefix { get; set; } = "[My Morning News]";

        /// <summary>
        /// Mailgun sending domain.
        /// </summary>
        [Setting(Description = "Mailgun sending domain.", EnvironmentVariable = "MAILGUN_DOMAIN")]
        public string MailgunDomain { get; set; } = String.Empty;

        /// <summary>
        /// Mailgun API key. Masked by the settings API; prefer the environment variable.
        /// </summary>
        [Setting(Description = "Mailgun API key.", Secret = true, EnvironmentVariable = "MAILGUN_API_KEY")]
        public string MailgunApiKey { get; set; } = String.Empty;

        /// <summary>
        /// Mailgun API base URL. Default https://api.mailgun.net/v3/ (EU: https://api.eu.mailgun.net/v3/).
        /// </summary>
        [Setting(Description = "Mailgun API base URL.", EnvironmentVariable = "MAILGUN_BASE_URL")]
        public string MailgunBaseUrl { get; set; } = "https://api.mailgun.net/v3/";

        /// <summary>
        /// Directory the File provider writes to. Default ./email (compose sets /data/email).
        /// </summary>
        [Setting(Description = "Directory for the File provider.", EnvironmentVariable = "EMAIL_FILE_DIRECTORY")]
        public string FileDirectory { get; set; } = "./email";

        /// <summary>
        /// Days an email link token stays valid. Default 30, minimum 1, maximum 365.
        /// </summary>
        [Setting(Description = "Days an email link stays valid.", Minimum = 1, Maximum = 365)]
        public int ActionTokenLifetimeDays
        {
            get { return _ActionTokenLifetimeDays; }
            set { _ActionTokenLifetimeDays = Math.Clamp(value, 1, 365); }
        }

        /// <summary>
        /// Record a rating when a link is opened (GET) instead of on submit. Default false, because mail scanners follow links.
        /// </summary>
        [Setting(Description = "Record feedback on link open (mail scanners may trigger it).")]
        public bool RecordFeedbackOnClick { get; set; } = false;

        /// <summary>
        /// Delivery attempts before an email fails. Default 5, minimum 1, maximum 20.
        /// </summary>
        [Setting(Description = "Delivery attempts before an email fails.", Minimum = 1, Maximum = 20)]
        public int DeliveryMaxAttempts
        {
            get { return _DeliveryMaxAttempts; }
            set { _DeliveryMaxAttempts = Math.Clamp(value, 1, 20); }
        }

        /// <summary>
        /// Seconds between delivery passes. Default 15, minimum 1, maximum 600.
        /// </summary>
        [Setting(Description = "Seconds between delivery passes.", Minimum = 1, Maximum = 600)]
        public int DeliveryIntervalSeconds
        {
            get { return _DeliveryIntervalSeconds; }
            set { _DeliveryIntervalSeconds = Math.Clamp(value, 1, 600); }
        }

        /// <summary>
        /// Days to keep sent emails (and their rendered copies). Default 90, minimum 1, maximum 3650.
        /// </summary>
        [Setting(Description = "Days to keep sent emails.", Minimum = 1, Maximum = 3650)]
        public int MessageRetentionDays
        {
            get { return _MessageRetentionDays; }
            set { _MessageRetentionDays = Math.Clamp(value, 1, 3650); }
        }

        /// <summary>
        /// Public feedback page requests allowed per IP address per minute. Default 60, minimum 5, maximum 10000.
        /// </summary>
        [Setting(Description = "Public feedback page requests per IP per minute.", Minimum = 5, Maximum = 10000)]
        public int PublicRequestsPerMinute
        {
            get { return _PublicRequestsPerMinute; }
            set { _PublicRequestsPerMinute = Math.Clamp(value, 5, 10000); }
        }

        #endregion

        #region Private-Members

        private int _ActionTokenLifetimeDays = 30;
        private int _DeliveryMaxAttempts = 5;
        private int _DeliveryIntervalSeconds = 15;
        private int _MessageRetentionDays = 90;
        private int _PublicRequestsPerMinute = 60;

        #endregion
    }
}
