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

        /// <summary>
        /// Days an account deletion waits before the data is removed (Hosting.DeletionGraceDays); signing in during them
        /// cancels it. Default 7.
        /// </summary>
        public int DeletionGraceDays { get; set; } = 7;

        /// <summary>
        /// Public base URL of the web dashboard (Rest.AppBaseUrl), for example to open the web sign-up page when the app
        /// cannot show the Turnstile challenge itself.
        /// </summary>
        public string AppUrl { get; set; } = "http://127.0.0.1:12101";

        /// <summary>
        /// Public URL of the MCP server's Streamable HTTP endpoint, or null (left out of the JSON) when the server
        /// knows no address a client could reach, for example a public server whose MCP URL is still a loopback
        /// address. Default null.
        /// </summary>
        public string? McpUrl { get; set; } = null;

        /// <summary>
        /// Cloudflare Turnstile site key for the sign-up form, or null when the check is off.
        /// </summary>
        public string? TurnstileSiteKey { get; set; } = null;

        /// <summary>
        /// Whether sign-up requires email verification.
        /// </summary>
        public bool RequireEmailVerification { get; set; } = true;

        /// <summary>
        /// Minimum age to sign up.
        /// </summary>
        public int MinimumAge { get; set; } = 18;

        /// <summary>
        /// Current terms of service version.
        /// </summary>
        public string TermsVersion { get; set; } = String.Empty;

        /// <summary>
        /// Whether the server has the operator's platform model key (`MYMORNINGNEWS_ANTHROPIC_API_KEY`), so endpoints that
        /// reference `platform:anthropic` can call the model. Never carries the key itself.
        /// </summary>
        public bool PlatformModelKeyConfigured { get; set; } = false;
    }
}
