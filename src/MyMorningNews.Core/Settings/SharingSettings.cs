namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Public share links for briefings (plan section 36).
    /// </summary>
    public class SharingSettings
    {
        /// <summary>
        /// Let users create public share links, and serve the public pages. Turning it off makes every existing link
        /// answer 404 until it is turned back on. Default true.
        /// </summary>
        [Setting(Description = "Let users share briefings through public links.", EnvironmentVariable = "SHARING_ENABLED")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Longest life of a share link in days. Default 14, minimum 1, maximum 365.
        /// </summary>
        [Setting(Description = "Longest life of a share link (days).", Minimum = 1, Maximum = 365, EnvironmentVariable = "SHARING_MAX_EXPIRY_DAYS")]
        public int MaxExpiryDays
        {
            get { return _MaxExpiryDays; }
            set { _MaxExpiryDays = Math.Clamp(value, 1, 365); }
        }

        /// <summary>
        /// Life of a share link in days when the request does not say. Never more than MaxExpiryDays. Default 7, minimum 1, maximum 365.
        /// </summary>
        [Setting(Description = "Life of a share link when the request does not say (days).", Minimum = 1, Maximum = 365)]
        public int DefaultExpiryDays
        {
            get { return _DefaultExpiryDays; }
            set { _DefaultExpiryDays = Math.Clamp(value, 1, 365); }
        }

        /// <summary>
        /// Public share page requests allowed per IP address per minute. Audio streaming is not counted. Default 120, minimum 5, maximum 10000.
        /// </summary>
        [Setting(Description = "Public share page requests per IP per minute.", Minimum = 5, Maximum = 10000)]
        public int PublicRequestsPerMinute
        {
            get { return _PublicRequestsPerMinute; }
            set { _PublicRequestsPerMinute = Math.Clamp(value, 5, 10000); }
        }

        private int _MaxExpiryDays = 14;
        private int _DefaultExpiryDays = 7;
        private int _PublicRequestsPerMinute = 120;
    }
}
