namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Create a user.
    /// </summary>
    public class UserCreateRequest
    {
        /// <summary>
        /// Email (unique in the tenant).
        /// </summary>
        [TextLimit(256)]
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Initial password (minimum 8 characters).
        /// </summary>
        public string Password { get; set; } = String.Empty;

        /// <summary>
        /// First name.
        /// </summary>
        [TextLimit(128)]
        public string? FirstName { get; set; } = null;

        /// <summary>
        /// Last name.
        /// </summary>
        [TextLimit(128)]
        public string? LastName { get; set; } = null;

        /// <summary>
        /// Global administrator (only global administrators may set this).
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Time zone.
        /// </summary>
        [TextLimit(64)]
        public string? TimeZone { get; set; } = null;

        /// <summary>
        /// Locale.
        /// </summary>
        [TextLimit(16)]
        public string? Locale { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}
