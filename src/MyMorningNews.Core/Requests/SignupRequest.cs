namespace MyMorningNews.Core.Requests
{
    /// <summary>
    /// Public sign-up (POST /v1.0/public/signup).
    /// </summary>
    public class SignupRequest
    {
        /// <summary>
        /// Email address (required).
        /// </summary>
        public string? Email { get; set; } = null;

        /// <summary>
        /// Password, at least 8 characters (required).
        /// </summary>
        public string? Password { get; set; } = null;

        /// <summary>
        /// Display name. Defaults to the part of the email before @.
        /// </summary>
        public string? DisplayName { get; set; } = null;

        /// <summary>
        /// IANA time zone. Default UTC.
        /// </summary>
        public string? TimeZone { get; set; } = null;

        /// <summary>
        /// Locale, for example en or fr. Default en.
        /// </summary>
        public string? Locale { get; set; } = null;

        /// <summary>
        /// The terms of service were accepted (required true).
        /// </summary>
        public bool AcceptTerms { get; set; } = false;

        /// <summary>
        /// The person confirms the minimum age (Hosting.MinimumAge, default 18) (required true).
        /// </summary>
        public bool ConfirmAge { get; set; } = false;

        /// <summary>
        /// Optional promotional code, redeemed for the new account.
        /// </summary>
        public string? PromoCode { get; set; } = null;

        /// <summary>
        /// Cloudflare Turnstile response, when the server requires it.
        /// </summary>
        public string? TurnstileToken { get; set; } = null;
    }
}
