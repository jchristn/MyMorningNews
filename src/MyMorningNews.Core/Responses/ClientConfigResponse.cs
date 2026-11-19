namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Public configuration for web and mobile clients (no authentication required).
    /// </summary>
    public class ClientConfigResponse
    {
        /// <summary>
        /// Product name.
        /// </summary>
        public string ProductName { get; set; } = String.Empty;

        /// <summary>
        /// Server version.
        /// </summary>
        public string ServerVersion { get; set; } = String.Empty;

        /// <summary>
        /// Whether public sign-up is open.
        /// </summary>
        public bool SignupEnabled { get; set; } = false;

        /// <summary>
        /// Whether the server sends push notifications.
        /// </summary>
        public bool PushEnabled { get; set; } = false;

        /// <summary>
        /// Whether the server makes audio (tts.enabled).
        /// </summary>
        public bool AudioEnabled { get; set; } = false;

        /// <summary>
        /// Whether the server sends email (email.enabled and a provider other than Disabled).
        /// </summary>
        public bool EmailEnabled { get; set; } = false;

        /// <summary>
        /// Oldest mobile app version this server supports.
        /// </summary>
        public string MinimumAppVersion { get; set; } = "0.1.0";

        /// <summary>
        /// Legal and support links keyed by privacy, terms, support, and deleteAccount.
        /// </summary>
        public Dictionary<string, string> Links { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Copyright notice.
        /// </summary>
        public string Copyright { get; set; } = "(c) 2026 Lattice AI LLC";
    }
}
