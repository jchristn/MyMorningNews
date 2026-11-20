namespace MyMorningNews.Core.Requests
{
    /// <summary>
    /// A request carrying a link token (verify email), with a new password for resets.
    /// </summary>
    public class AccountTokenRequest
    {
        /// <summary>
        /// Token from the emailed link.
        /// </summary>
        public string? Token { get; set; } = null;

        /// <summary>
        /// New password (password reset only).
        /// </summary>
        public string? Password { get; set; } = null;
    }
}
