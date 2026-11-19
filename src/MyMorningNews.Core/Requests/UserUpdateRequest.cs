namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Update a user. Null properties are unchanged.
    /// </summary>
    public class UserUpdateRequest
    {
        /// <summary>
        /// Email.
        /// </summary>
        public string? Email { get; set; } = null;

        /// <summary>
        /// New password (administrator reset).
        /// </summary>
        public string? Password { get; set; } = null;

        /// <summary>
        /// First name.
        /// </summary>
        public string? FirstName { get; set; } = null;

        /// <summary>
        /// Last name.
        /// </summary>
        public string? LastName { get; set; } = null;

        /// <summary>
        /// Global administrator.
        /// </summary>
        public bool? IsAdmin { get; set; } = null;

        /// <summary>
        /// Tenant administrator.
        /// </summary>
        public bool? IsTenantAdmin { get; set; } = null;

        /// <summary>
        /// Time zone.
        /// </summary>
        public string? TimeZone { get; set; } = null;

        /// <summary>
        /// Locale.
        /// </summary>
        public string? Locale { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
