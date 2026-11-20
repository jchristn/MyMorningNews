namespace MyMorningNews.Core.Requests
{
    /// <summary>
    /// Optional body for account deletion. When a password is given it must match, as a second check that the person
    /// holding the session owns the account.
    /// </summary>
    public class AccountDeleteRequest
    {
        /// <summary>
        /// Current password, or null to rely on the session alone.
        /// </summary>
        public string? Password { get; set; } = null;

        /// <summary>
        /// Explicit confirmation flag sent by the apps. Informational; deletion proceeds either way.
        /// </summary>
        public bool? Confirm { get; set; } = null;
    }
}
