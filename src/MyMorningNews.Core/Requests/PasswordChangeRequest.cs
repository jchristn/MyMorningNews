namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Change one's own password.
    /// </summary>
    public class PasswordChangeRequest
    {
        /// <summary>
        /// Current password.
        /// </summary>
        public string CurrentPassword { get; set; } = String.Empty;

        /// <summary>
        /// New password (minimum 8 characters).
        /// </summary>
        public string NewPassword { get; set; } = String.Empty;
    }
}
