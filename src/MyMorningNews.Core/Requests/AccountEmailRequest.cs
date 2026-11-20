namespace MyMorningNews.Core.Requests
{
    /// <summary>
    /// A request that names an email address (resend verification, forgot password).
    /// </summary>
    public class AccountEmailRequest
    {
        /// <summary>
        /// Email address.
        /// </summary>
        public string? Email { get; set; } = null;
    }
}
